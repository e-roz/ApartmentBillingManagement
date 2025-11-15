using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Pages
{

    [Authorize(Roles = "Admin,Manager")]
    public class ManageApartmentsModel : PageModel
    {
        private readonly ApplicationDbContext dbData;


        // BindProperty for the form data (Used for Add/Edit)
        [BindProperty]
        public ApartmentModel ApartmentInput { get; set; } = new ApartmentModel();

        // Separate property for tenant selection (since ApartmentModel no longer has TenantId)
        [BindProperty]
        public int? SelectedTenantId { get; set; }

        //property to hold the search term
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }


        //holding the list of available apartments for the main table view
        public List<ApartmentList> Apartments { get; set; } = new List<ApartmentList>();

        //hold the list of available tenants for the dropdown
        public SelectList AvailableTenants { get; set; }


        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public ManageApartmentsModel(ApplicationDbContext context)
        {
            dbData = context;
        }

        // -- Core Logic for the Manage Apartments Page will go here --

        public async Task OnGetAsync()
        {
            // Get all apartments
            var apartmentQuery = dbData.Apartments.AsQueryable();

            // Get all active tenants with their apartments
            var activeTenants = await dbData.Tenants
                .Where(t => t.Status == LeaseStatus.Active && t.ApartmentId.HasValue)
                .Include(t => t.Apartment)
                .ToListAsync();

            // Create a dictionary for quick lookup: ApartmentId -> Tenant
            var tenantByApartmentId = activeTenants
                .Where(t => t.Apartment != null)
                .ToDictionary(t => t.Apartment!.Id, t => t);

            // search logic filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                string term = SearchTerm.Trim();
                var matchingApartmentIds = activeTenants
                    .Where(t => t.Apartment != null && t.FullName.Contains(term))
                    .Select(t => t.Apartment!.Id)
                    .ToList();

                apartmentQuery = apartmentQuery.Where(a =>
                    a.UnitNumber.Contains(term) ||
                    matchingApartmentIds.Contains(a.Id)
                );
            }

            // execute composed query
            var apartmentEntities = await apartmentQuery
                .OrderBy(a => a.UnitNumber)
                .ToListAsync();

            //Map to ApartmentList for display and sync IsOccupied flag
            Apartments = apartmentEntities.Select(a =>
            {
                var tenant = tenantByApartmentId.GetValueOrDefault(a.Id);
                bool isOccupied = tenant != null;
                
                // Sync IsOccupied flag with actual tenant status
                if (a.IsOccupied != isOccupied)
                {
                    a.IsOccupied = isOccupied;
                    dbData.Apartments.Update(a);
                }
                
                return new ApartmentList
                {
                    Id = a.Id,
                    UnitNumber = a.UnitNumber,
                    MonthlyRent = a.MonthlyRent,
                    StatusDisplay = isOccupied ? "Occupied" : "Vacant",
                    TenantName = tenant?.FullName ?? "N/A",
                    TenantId = tenant?.Id
                };
            }).ToList();
            
            // Save any IsOccupied flag updates
            await dbData.SaveChangesAsync();

            //Load available tenants for the dropdown
            await LoadAvailableTenantsAsync();
        }

        private async Task LoadAvailableTenantsAsync()
        {
            //Fetch tenants who are not currently assigned to any apartment (or are inactive)
            var unassignedTenants = await dbData.Tenants
                .Where(t => !t.ApartmentId.HasValue || t.Status != LeaseStatus.Active)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            //create a select list for the dropdown
            var selectListItems = unassignedTenants
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.FullName
                })
                .ToList();

            // Add a default "Vacant" option. This is how we handle unassignment/vacant status.
            selectListItems.Insert(0, new SelectListItem { Value = "0", Text = "Vacant", Selected = true });
            AvailableTenants = new SelectList(selectListItems, "Value", "Text");
        }


        //ADD APARTMENT LOGIC
        public async Task<IActionResult> OnPostAddApartmentAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync(); //Reload data for the page
                ErrorMessage = "Invalid apartment data. Please check the inputs.";
                return RedirectToPage();
            }
            //Ensure UnitNumber is unique
            if(await dbData.Apartments.AnyAsync(a => a.UnitNumber == ApartmentInput.UnitNumber))
            {
                ModelState.AddModelError("ApartmentInput.UnitNumber", "An apartment with this unit number already exists.");
                await OnGetAsync();
                ErrorMessage = "A unit with this number already exists";
                return Page();
            }
          
            // Set IsOccupied based on whether there's an active tenant for this apartment
            // (This will be updated when a tenant is assigned)
            ApartmentInput.IsOccupied = false;

            dbData.Apartments.Add(ApartmentInput);
            await dbData.SaveChangesAsync();

            // If SelectedTenantId was provided, assign the tenant
            if (SelectedTenantId.HasValue && SelectedTenantId.Value > 0)
            {
                var tenant = await dbData.Tenants.FindAsync(SelectedTenantId.Value);
                if (tenant != null)
                {
                    tenant.ApartmentId = ApartmentInput.Id;
                    ApartmentInput.IsOccupied = true;
                    dbData.Tenants.Update(tenant);
                    dbData.Apartments.Update(ApartmentInput);
                    await dbData.SaveChangesAsync();
                }
            }

            SuccessMessage = $"Apartment '{ApartmentInput.UnitNumber}' added successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteApartmentAsync(int ApartmentId)
        {
            // Include Bills to perform the safety check
            var apartmentToDelete = await dbData.Apartments
                .Include(a => a.Bills)
                .FirstOrDefaultAsync(a => a.Id == ApartmentId);

            if(apartmentToDelete == null)
            {
                ErrorMessage = "Apartment not found.";
                return RedirectToPage();
            }

            // Check if apartment is occupied by an active tenant
            var activeTenant = await dbData.Tenants
                .FirstOrDefaultAsync(t => t.ApartmentId == ApartmentId && t.Status == LeaseStatus.Active);

            if (activeTenant != null)
            {
                ErrorMessage = $"Cannot delete unit '{apartmentToDelete.UnitNumber}' because it is currently occupied by {activeTenant.FullName}. Please vacate the unit first.";
                return RedirectToPage();
            }

            // Safety check: check for associated bills
            if(apartmentToDelete.Bills != null && apartmentToDelete.Bills.Any())
            {
                ErrorMessage = $"Cannot delete unit '{apartmentToDelete.UnitNumber}' because it has associated bills. Please resolve them first.";
                return RedirectToPage();
            }

            try
            {
                dbData.Apartments.Remove(apartmentToDelete);
                await dbData.SaveChangesAsync();

                SuccessMessage = $"Apartment '{apartmentToDelete.UnitNumber}' deleted successfully.";
                return RedirectToPage();
            }
            catch(Exception ex) 
            {
                ErrorMessage = $"An error occurred while deleting the apartment: {ex.Message}";
                return RedirectToPage();
            }              
        }

        //Post handler for assigning a tenant to an apartment unit
        public async Task<IActionResult> OnPostAssignTenantAsync(int apartmentId, int tenantId)
        {
            var apartment = await dbData.Apartments.FindAsync(apartmentId);
            var tenant = await dbData.Tenants.FindAsync(tenantId);

            if (apartment == null || tenant == null)
            {
                ErrorMessage = "Apartment or Tenant not found.";
                return RedirectToPage();
            }

            // Check if the apartment is already occupied by a different active tenant
            var existingTenant = await dbData.Tenants
                .FirstOrDefaultAsync(t => t.ApartmentId == apartmentId && t.Status == LeaseStatus.Active && t.Id != tenantId);

            if (existingTenant != null)
            {
                // Unassign the previous tenant
                existingTenant.ApartmentId = null;
                dbData.Tenants.Update(existingTenant);
                SuccessMessage = $"Apartment {apartment.UnitNumber} was previously occupied by {existingTenant.FullName}. Tenant has been updated.";
            }
            else
            {
                SuccessMessage = $"Tenant '{tenant.FullName}' assigned to apartment '{apartment.UnitNumber}' successfully.";
            }

            // Assign the new tenant
            tenant.ApartmentId = apartmentId;
            apartment.IsOccupied = true;

            dbData.Tenants.Update(tenant);
            dbData.Apartments.Update(apartment);
            await dbData.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostVacateApartmentAsync(int id)
        {
            var apartment = await dbData.Apartments.FindAsync(id);

            if(apartment == null)
            {
                ErrorMessage = "Apartment not found for vacating";
                return RedirectToPage();
            }

            // Find and unassign any active tenant from this apartment
            var tenant = await dbData.Tenants
                .FirstOrDefaultAsync(t => t.ApartmentId == id && t.Status == LeaseStatus.Active);

            if (tenant != null)
            {
                tenant.ApartmentId = null;
                dbData.Tenants.Update(tenant);
            }

            apartment.IsOccupied = false;
            dbData.Apartments.Update(apartment);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"Apartment '{apartment.UnitNumber}' has been marked as vacant.";
            return RedirectToPage();
        }

        // EDIT APARTMENT LOGIC
        public async Task<IActionResult> OnPostEditApartmentAsync(int ApartmentId, string UnitNumber, decimal MonthlyRent, int? TenantId)
        {
            if (string.IsNullOrWhiteSpace(UnitNumber) || MonthlyRent < 0)
            {
                ErrorMessage = "Invalid apartment data. Please check the inputs.";
                return RedirectToPage();
            }

            var apartment = await dbData.Apartments.FirstOrDefaultAsync(a => a.Id == ApartmentId);
            if (apartment == null)
            {
                ErrorMessage = "Apartment not found.";
                return RedirectToPage();
            }

            // Enforce unique UnitNumber excluding current record
            bool unitExists = await dbData.Apartments
                .AnyAsync(a => a.UnitNumber == UnitNumber && a.Id != ApartmentId);
            if (unitExists)
            {
                ErrorMessage = "Another apartment with this unit number already exists.";
                return RedirectToPage();
            }

            apartment.UnitNumber = UnitNumber;
            apartment.MonthlyRent = MonthlyRent;

            // Handle tenant assignment/unassignment
            if (TenantId.HasValue && TenantId.Value > 0)
            {
                var tenant = await dbData.Tenants.FindAsync(TenantId.Value);
                if (tenant != null)
                {
                    // Unassign any existing tenant from this apartment
                    var existingTenant = await dbData.Tenants
                        .FirstOrDefaultAsync(t => t.ApartmentId == ApartmentId && t.Status == LeaseStatus.Active && t.Id != TenantId.Value);
                    if (existingTenant != null)
                    {
                        existingTenant.ApartmentId = null;
                        dbData.Tenants.Update(existingTenant);
                    }

                    // Assign the new tenant
                    tenant.ApartmentId = ApartmentId;
                    apartment.IsOccupied = true;
                    dbData.Tenants.Update(tenant);
                }
            }
            else
            {
                // Unassign any existing tenant
                var existingTenant = await dbData.Tenants
                    .FirstOrDefaultAsync(t => t.ApartmentId == ApartmentId && t.Status == LeaseStatus.Active);
                if (existingTenant != null)
                {
                    existingTenant.ApartmentId = null;
                    dbData.Tenants.Update(existingTenant);
                }
                apartment.IsOccupied = false;
            }

            dbData.Apartments.Update(apartment);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"Apartment '{apartment.UnitNumber}' updated successfully.";
            return RedirectToPage();
        }

    }
}
