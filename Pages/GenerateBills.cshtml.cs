using Apartment.Data;
using Apartment.Model;
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

            if (!selectedApartmentIds.Any())
            {
                ModelState.AddModelError("Input.SelectedApartmentIds", "Please select at least one occupied apartment for billing.");
                await LoadOccupiedApartmentsAsync();
                return Page();
            }

            if (!Input.DueDate.HasValue)
            {
                ModelState.AddModelError("Input.DueDate", "Please choose a due date for the generated bills.");
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

            if (!activeTenants.Any())
            {
                ModelState.AddModelError(string.Empty, "No active tenants found. Bills can only be generated for tenants with Active lease status.");
                await LoadOccupiedApartmentsAsync();
                return Page();
            }

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

            var billsCreate = new List<Bill>();
            var dueDate = Input.DueDate.Value;

            // Generate bills for each active tenant
            foreach (var tenant in tenantsToBill)
            {
                // Check if a Bill already exists for this TenantId and the current BillingPeriodId
                bool billExists = await dbData.Bills
                    .AnyAsync(b => b.TenantId == tenant.Id && b.BillingPeriodId == billingPeriod.Id);

                if (billExists)
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
                        GeneratedDate = DateTime.Now,
                        PaymentDate = null
                    };
                    billsCreate.Add(newBill);
                }
            }
            // Save all generated bills to the database
            dbData.Bills.AddRange(billsCreate);
            await dbData.SaveChangesAsync();

            // Prepare summary
            Summary = new GenerationSummary
            {
                PeriodKey = periodKey,
                BillsCreated = billsCreate.Count,
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
