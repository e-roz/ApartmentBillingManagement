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

namespace Apartment.Pages.Admin
{

    [Authorize(Roles = "Admin")]
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

        public List<ViewModels.ApartmentList> OccupiedApartments { get; set; } = new List<ViewModels.ApartmentList>();

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
            var now = DateTime.UtcNow;
            // Get active leases with users and apartments
            var activeLeases = await dbData.Leases
                .Include(l => l.User)
                .Include(l => l.Apartment)
                .Where(l => l.LeaseEnd >= now && 
                           (l.User.Status == "Active" || l.User.Status == null))
                .ToListAsync();

            OccupiedApartments = activeLeases
                .Select(l => new ViewModels.ApartmentList
                {
                    Id = l.Apartment.Id,
                    UnitNumber = l.Apartment.UnitNumber,
                    TenantName = l.User.Username,
                    MonthlyRent = l.MonthlyRent // use lease-level monthly rent
                })
                .DistinctBy(a => a.Id) // Remove duplicates if same apartment has multiple leases
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

                // Fetch Active Leases: Query for all active leases
                var now = DateTime.UtcNow;
                var activeLeases = await dbData.Leases
                    .Include(l => l.User)
                    .Include(l => l.Apartment)
                    .Where(l => l.LeaseEnd >= now && 
                               (l.User.Status == "Active" || l.User.Status == null))
                    .ToListAsync();


                // Filter by selected apartment IDs if provided
                var leasesToBill = activeLeases
                    .Where(l => selectedApartmentIds.Contains(l.ApartmentId))
                    .ToList();

                if (!leasesToBill.Any())
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("Input.SelectedApartmentIds", "No active leases matched the selected units. Please review your selection.");
                    await LoadOccupiedApartmentsAsync();
                    return Page();
                }
                // Re-check for existing bills within transaction to prevent race conditions
                var existingTenantUserIdsWithBills = await dbData.Bills
                    .Where(b => b.BillingPeriodId == billingPeriod.Id && b.Type == BillType.Rent)
                    .Select(b => b.TenantUserId)
                    .ToListAsync();

                var billedTenantUserIds = new HashSet<int>(existingTenantUserIdsWithBills);

                // Track newly created RENT bills for this generation run
                var rentBillsCreate = new List<Bill>();
                var dueDate = Input.DueDate.Value;

                // --- Step 1: Generate RENT bills for the selected period ---
                foreach (var lease in leasesToBill)
                {
                    // Skip if a bill for this tenant & period already exists for rent
                    if (billedTenantUserIds.Contains(lease.UserId))
                    {
                        continue;
                    }

                    // Create a new RENT bill
                    var newRentBill = new Bill
                    {
                        ApartmentId = lease.ApartmentId,
                        TenantUserId = lease.UserId,
                        BillingPeriodId = billingPeriod.Id,
                        LeaseId = lease.Id,
                        AmountDue = lease.MonthlyRent,
                        DueDate = dueDate,
                        GeneratedDate = DateTime.UtcNow,
                        Status = BillStatus.Unpaid,
                        Type = BillType.Rent, // Explicitly set the type
                        Description = $"{monthName} {Input.Year} Rent"
                    };
                    rentBillsCreate.Add(newRentBill);
                }

                // First, save all newly created RENT bills so they get database IDs
                if (rentBillsCreate.Any())
                {
                    dbData.Bills.AddRange(rentBillsCreate);
                    await dbData.SaveChangesAsync();
                }

                // --- Step 2: Generate LATE FEE bills for any overdue rent (existing + newly created this run) ---

                var nowUtc = DateTime.UtcNow;

                // Find which overdue rent bills already have a late fee generated
                var parentBillIdsWithFees = await dbData.Bills
                    .Where(b => b.Type == BillType.LateFee && b.ParentBillId != null)
                    .Select(b => b.ParentBillId!.Value)
                    .ToListAsync();

                // 2a. EXISTING overdue rent bills in the database (unpaid, outside grace period)
                var existingOverdueRentBills = await dbData.Bills
                    .Include(b => b.Lease)
                    .Include(b => b.BillingPeriod)
                    .Where(b => b.Type == BillType.Rent &&
                                !parentBillIdsWithFees.Contains(b.Id) &&
                                b.AmountDue > b.PaymentAllocations.Sum(pa => pa.AmountApplied) && // is unpaid
                                b.DueDate.AddDays(b.Lease.LateFeeDays) < nowUtc) // is overdue beyond grace
                    .ToListAsync();

                // 2b. NEW rent bills from this generation run that are already overdue
                var newOverdueRentBills = rentBillsCreate
                    .Join(
                        leasesToBill,
                        bill => bill.LeaseId,
                        lease => lease.Id,
                        (bill, lease) => new { Bill = bill, Lease = lease }
                    )
                    .Where(x => x.Bill.DueDate.AddDays(x.Lease.LateFeeDays) < nowUtc &&
                                !parentBillIdsWithFees.Contains(x.Bill.Id))
                    .Select(x =>
                    {
                        // Ensure navigation properties are populated for consistency
                        x.Bill.Lease = x.Lease;
                        x.Bill.BillingPeriod = billingPeriod;
                        return x.Bill;
                    })
                    .ToList();

                // Combine existing + newly created overdue rent bills (avoid duplicates)
                var overdueRentBills = existingOverdueRentBills
                    .Concat(newOverdueRentBills)
                    .DistinctBy(b => b.Id)
                    .ToList();

                var lateFeeBillsCreate = new List<Bill>();

                foreach (var overdueBill in overdueRentBills)
                {
                    // Create a new LATE FEE bill
                    var newLateFeeBill = new Bill
                    {
                        ApartmentId = overdueBill.ApartmentId,
                        TenantUserId = overdueBill.TenantUserId,
                        BillingPeriodId = billingPeriod.Id, // Assign to the CURRENT generating period
                        LeaseId = overdueBill.LeaseId,
                        AmountDue = overdueBill.Lease.LateFeeAmount,
                        DueDate = dueDate, // Due on the same day as the new rent bills
                        GeneratedDate = DateTime.UtcNow,
                        Status = BillStatus.Unpaid,
                        Type = BillType.LateFee, // Explicitly set the type
                        ParentBillId = overdueBill.Id, // Link back to the overdue rent bill
                        Description = $"Late Fee for {overdueBill.BillingPeriod.MonthName} {overdueBill.BillingPeriod.Year} Rent"
                    };
                    lateFeeBillsCreate.Add(newLateFeeBill);
                }

                if (lateFeeBillsCreate.Any())
                {
                    dbData.Bills.AddRange(lateFeeBillsCreate);
                    await dbData.SaveChangesAsync();
                }

                // Log and commit transaction
                var totalBillsCreated = rentBillsCreate.Count + lateFeeBillsCreate.Count;

                if (totalBillsCreated > 0)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out var userId))
                    {
                        var details = $"Generated {totalBillsCreated} bills for period {periodKey}.";
                        await _auditService.LogAsync(AuditActionType.GenerateBills, userId, details, billingPeriod.Id, nameof(BillingPeriod));
                    }
                }

                await transaction.CommitAsync();

                // Prepare summary - bills were saved within transaction
                Summary = new GenerationSummary
                {
                    PeriodKey = periodKey,
                    BillsCreated = totalBillsCreated,
                    OccupiedUnits = leasesToBill.Select(l => l.ApartmentId).Distinct().Count(),
                    AlreadyExists = false,
                    TotalAmountBilled = rentBillsCreate.Sum(b => b.AmountDue) + lateFeeBillsCreate.Sum(b => b.AmountDue)
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
                ModelState.AddModelError(string.Empty, $"Error saving bills to database: {ex.ToString()}");
                await LoadOccupiedApartmentsAsync();
                return Page();
            }
        }
    }
}
