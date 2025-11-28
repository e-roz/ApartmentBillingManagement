using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System;

namespace Apartment.Pages.Manager
{

    [Authorize(Roles = "Manager")]
    public class GenerateBillsModel : PageModel
    {
        private readonly ApplicationDbContext dbData;
        private readonly ILogSnagClient _logSnagClient;
        private readonly IAuditService _auditService;
        private static readonly CultureInfo PhpCulture = CultureInfo.CreateSpecificCulture("en-PH");
        public GenerateBillsModel(ApplicationDbContext context, ILogSnagClient logSnagClient, IAuditService auditService)
        {
            dbData = context;
            _logSnagClient = logSnagClient;
            _auditService = auditService;
        }

        //Input model to hold the selected month/year for bill generation
        [BindProperty]
        public BillingInput Input { get; set; } = new BillingInput();

        public List<ApartmentList> OccupiedApartments { get; set; } = new List<ApartmentList>();

        // Using regular properties instead of TempData since we return Page() not Redirect
        // TempData would persist to the next page visit if not consumed
        public string SuccessMessage { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        // Model to display the results of the generation process
        public class GenerationSummary
        {
            public string PeriodKey { get; set; } = string.Empty;
            public int BillsCreated { get; set; }
            public int OccupiedUnits { get; set; }
            public bool AlreadyExists { get; set; }
            public decimal TotalAmountBilled { get; set; }
        }

        public GenerationSummary? Summary { get; set; }


        //input used for the form
        public class BillingInput
        {
            [BindProperty, Required]
            public int Year { get; set; } = DateTime.Now.Year;

            [BindProperty, Required]
            [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
            public int Month { get; set; } = DateTime.Now.Month;

            public List<int> SelectedApartmentIds { get; set; } = new List<int>();

            [BindProperty, Required]
            [DataType(DataType.Date)]
            public DateTime? DueDate { get; set; }
        }

        private async Task LoadOccupiedApartmentsAsync()
        {
            // Get active tenants with apartments assigned
            var activeTenants = await dbData.Tenants
                .Include(t => t.Apartment)
                .Where(t => t.Status == LeaseStatus.Active && t.ApartmentId.HasValue)
                .ToListAsync();

            OccupiedApartments = activeTenants
                .Where(t => t.Apartment != null)
                .Select(t => new ApartmentList
                {
                    Id = t.Apartment!.Id,
                    UnitNumber = t.Apartment.UnitNumber,
                    TenantName = t.FullName,
                    MonthlyRent = t.MonthlyRent
                })
                .ToList();
        }

        public async Task OnGetAsync()
        {
            DateTime nextMonth = DateTime.Now.AddMonths(1);
            Input.Year = nextMonth.Year;
            Input.Month = nextMonth.Month;
            Input.DueDate = new DateTime(nextMonth.Year, nextMonth.Month, 5);

            await LoadOccupiedApartmentsAsync();

            //default select all occupied apartments for billing
            Input.SelectedApartmentIds = OccupiedApartments.Select(a => a.Id).ToList();
        }


        public async Task<IActionResult> OnPostGenerateAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadOccupiedApartmentsAsync();
                return Page();
            }
            // define muna target billing and period brad 
            var selectedApartmentIds = (Input.SelectedApartmentIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();

            if (!selectedApartmentIds.Any() || !Input.DueDate.HasValue)
            {
                if (!selectedApartmentIds.Any()) ModelState.AddModelError("Input.SelectedApartmentIds", "Please select at least one occupied apartment for billing.");
                if (!Input.DueDate.HasValue) ModelState.AddModelError("Input.DueDate", "Please choose a due date for the generated bills.");
                await LoadOccupiedApartmentsAsync();
                return Page();
            }

            DateTime targetDate = new DateTime(Input.Year, Input.Month, 1);
            string periodKey = targetDate.ToString("yyyy-MM");
            string monthName = targetDate.ToString("MMMM"); // Full month name

            // Use transaction to prevent race conditions
            await using var transaction = await dbData.Database.BeginTransactionAsync();
            try
            {
                // Check if billing period already exists (within transaction to prevent race condition)
                var existingPeriod = await dbData.BillingPeriods
                    .FirstOrDefaultAsync(bp => bp.PeriodKey == periodKey);

                if (existingPeriod != null)
                {
                    // check if any bills have already been generated for this period
                    bool biilsAlreadyGenerated = await dbData.Bills
                        .AnyAsync(b => b.BillingPeriodId == existingPeriod.Id);

                    if (biilsAlreadyGenerated)
                    {
                        await transaction.RollbackAsync();
                        Summary = new GenerationSummary
                        {
                            PeriodKey = periodKey,
                            BillsCreated = 0,
                            OccupiedUnits = 0,
                            AlreadyExists = true,
                            TotalAmountBilled = 0
                        };
                        ModelState.AddModelError(string.Empty, $"Bills for {monthName} {Input.Year} have already been generated and exist in the system.");
                        await LoadOccupiedApartmentsAsync();
                        return Page();
                    }
                }

                // find or create billing period (within transaction)
                BillingPeriod billingPeriod;
                if (existingPeriod == null)
                {
                    billingPeriod = new BillingPeriod
                    {
                        PeriodKey = periodKey,
                        MonthName = monthName,
                        Year = Input.Year
                    };
                    dbData.BillingPeriods.Add(billingPeriod);
                    await dbData.SaveChangesAsync();
                }
                else
                {
                    billingPeriod = existingPeriod;
                }

                // Fetch Active Leases: Query for all Tenant records where Status is Active
                var activeTenants = await dbData.Tenants
                    .Include(t => t.Apartment)
                    .Where(t => t.Status == LeaseStatus.Active && t.ApartmentId.HasValue)
                    .ToListAsync();


                // Filter by selected apartment IDs if provided
                var tenantsToBill = activeTenants
                    .Where(t => t.Apartment != null && selectedApartmentIds.Contains(t.Apartment.Id))
                    .ToList();

                if (!tenantsToBill.Any())
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("Input.SelectedApartmentIds", "No active tenants matched the selected units. Please review your selection.");
                    await LoadOccupiedApartmentsAsync();
                    return Page();
                }
                // Re-check for existing bills within transaction to prevent race conditions
                var existingTenantIdsWithBills = await dbData.Bills
                    .Where(b => b.BillingPeriodId == billingPeriod.Id)
                    .Select(b => b.TenantId)
                    .ToListAsync();

                var billedTenantsIds = new HashSet<int>(existingTenantIdsWithBills);

                var billsCreate = new List<Bill>();
                var dueDate = Input.DueDate.Value;

                // Generate bills for each active tenant
                foreach (var tenant in tenantsToBill)
                {
                    // Check if a Bill already exists for this TenantId and the current BillingPeriodId
                    if (billedTenantsIds.Contains(tenant.Id))
                    {
                        // Skip this tenant if bill already exists
                        continue;
                    }

                    // Create a new Bill entity
                    if (tenant.Apartment != null)
                    {
                        var newBill = new Bill
                        {
                            ApartmentId = tenant.Apartment.Id,
                            TenantId = tenant.Id,
                            BillingPeriodId = billingPeriod.Id,
                            AmountDue = tenant.MonthlyRent, // Use Tenant.MonthlyRent
                            AmountPaid = 0.00m, // Will be calculated from invoices
                            DueDate = dueDate,
                            GeneratedDate = DateTime.UtcNow,
                            PaymentDate = null
                        };
                        billsCreate.Add(newBill);
                    }
                }

                // Save all generated bills to the database
                if (billsCreate.Any())
                {
                    dbData.Bills.AddRange(billsCreate);

                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out var userId))
                    {
                        var details = $"Generated {billsCreate.Count} bills for period {periodKey}.";
                        await _auditService.LogAsync(AuditActionType.GenerateBills, userId, details, billingPeriod.Id, nameof(BillingPeriod));
                    }
                    
                    await dbData.SaveChangesAsync();
                    await transaction.CommitAsync();
                }

                // Prepare summary - bills were saved within transaction
                Summary = new GenerationSummary
                {
                    PeriodKey = periodKey,
                    BillsCreated = billsCreate.Count,
                    OccupiedUnits = tenantsToBill.Count,
                    AlreadyExists = false,
                    TotalAmountBilled = billsCreate.Sum(b => b.AmountDue)
                };

                SuccessMessage = $"Successfully generated {Summary.BillsCreated} bills for {monthName} {Input.Year}. Total amount billed: {Summary.TotalAmountBilled.ToString("C", PhpCulture)}.";

                await _logSnagClient.PublishAsync(new LogSnagEvent
                {
                    Event = "Bills Generated",
                    Description = $"{Summary.BillsCreated} bills created for {monthName} {Input.Year}",
                    Icon = "🧾",
                    Tags = new Dictionary<string, string>
                    {
                        { "period", Summary.PeriodKey },
                        { "amount", Summary.TotalAmountBilled.ToString("0.##") }
                    }
                });

                await LoadOccupiedApartmentsAsync();
                return Page();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, $"Error saving bills to database: {ex.Message}");
                await LoadOccupiedApartmentsAsync();
                return Page();
            }
        }
    }
}
