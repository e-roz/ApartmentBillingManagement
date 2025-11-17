using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Apartment.Pages
{

    [Authorize(Roles = "Manager")]
    public class GenerateBillsModel : PageModel
    {
        private readonly ApplicationDbContext dbData;
        public GenerateBillsModel(ApplicationDbContext context)
        {
            dbData = context;
        }

        //Input model to hold the selected month/year for bill generation
        [BindProperty]
        public BillingInput Input { get; set; } = new BillingInput();

        public List<ApartmentList> OccupiedApartments { get; set; } = new List<ApartmentList>();

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        [TempData]
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


            // Check if billing period already exists
            var existingPeriod = await dbData.BillingPeriods
                .FirstOrDefaultAsync(bp => bp.PeriodKey == periodKey);

            if (existingPeriod != null)
            {
                // check if any bills have already been generated for this period
                bool biilsAlreadyGenerated = await dbData.Bills
                    .AnyAsync(b => b.BillingPeriodId == existingPeriod.Id);

                if (biilsAlreadyGenerated)
                {
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

            // find or create billing period
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
                ModelState.AddModelError("Input.SelectedApartmentIds", "No active tenants matched the selected units. Please review your selection.");
                await LoadOccupiedApartmentsAsync();
                return Page();
            }

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
                        AmountPaid = 0.00m,
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
                try
                {
                    dbData.Bills.AddRange(billsCreate);
                    int savedCount = await dbData.SaveChangesAsync();

                    // Verify bills were actually saved
                    var savedBillsCount = await dbData.Bills
                        .Where(b => b.BillingPeriodId == billingPeriod.Id && 
                                    billsCreate.Select(bc => bc.TenantId).Contains(b.TenantId))
                        .CountAsync();

                    if (savedBillsCount == 0)
                    {
                        ModelState.AddModelError(string.Empty, "Warning: Bills were created but may not have been saved to the database. Please verify the database connection.");
                        await LoadOccupiedApartmentsAsync();
                        return Page();
                    }

                    // Create corresponding Invoice records for each Bill
                    // Note: After SaveChangesAsync, EF Core assigns IDs to the bills in billsCreate
                    var invoicesCreate = new List<Invoice>();
                    foreach (var bill in billsCreate)
                    {
                        // Generate invoice title from billing period
                        var invoiceTitle = $"{billingPeriod.MonthName} {billingPeriod.Year} - Rent Invoice";

                        var newInvoice = new Invoice
                        {
                            BillId = bill.Id, // Link invoice to bill (Id is assigned after SaveChangesAsync)
                            TenantId = bill.TenantId,
                            ApartmentId = bill.ApartmentId,
                            Title = invoiceTitle,
                            AmountDue = bill.AmountDue,
                            DueDate = bill.DueDate,
                            IssueDate = DateTime.UtcNow,
                            Status = InvoiceStatus.Pending
                        };
                        invoicesCreate.Add(newInvoice);
                    }

                    // Save all generated invoices to the database
                    if (invoicesCreate.Any())
                    {
                        dbData.Invoices.AddRange(invoicesCreate);
                        await dbData.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error saving bills/invoices to database: {ex.Message}");
                    await LoadOccupiedApartmentsAsync();
                    return Page();
                }
            }

            // Prepare summary - verify actual saved count
            int actualSavedCount = 0;
            if (billsCreate.Any())
            {
                actualSavedCount = await dbData.Bills
                    .Where(b => b.BillingPeriodId == billingPeriod.Id && 
                                billsCreate.Select(bc => bc.TenantId).Contains(b.TenantId))
                    .CountAsync();
            }

            Summary = new GenerationSummary
            {
                PeriodKey = periodKey,
                BillsCreated = actualSavedCount > 0 ? actualSavedCount : billsCreate.Count,
                OccupiedUnits = tenantsToBill.Count,
                AlreadyExists = false,
                TotalAmountBilled = billsCreate.Sum(b => b.AmountDue)
            };

            SuccessMessage = $"Successfully generated {Summary.BillsCreated} bills for {monthName} {Input.Year}. Total amount billed: {Summary.TotalAmountBilled:C}.";

            await LoadOccupiedApartmentsAsync();
            return Page();

        }
    }
}
