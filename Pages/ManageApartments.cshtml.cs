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


            var apartmentQuery = dbData.Apartments
                .Include(a => a.Tenant)
                .AsQueryable();

            // search logic filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                string term = SearchTerm.Trim();
                apartmentQuery = apartmentQuery.Where(a =>
                    a.UnitNumber.Contains(term) ||
                    (a.TenantId.HasValue && a.Tenant != null && a.Tenant.Username.Contains(term))
                );
            }

            // execute composed query
            var apartmentEntities = await apartmentQuery
                .OrderBy(a => a.UnitNumber)
                .ToListAsync();




            //Map to ApartmentList for display
            Apartments = apartmentEntities.Select(a => new ApartmentList
            {
                Id = a.Id,
                UnitNumber = a.UnitNumber,
                MonthlyRent = a.MonthlyRent,
                StatusDisplay = a.IsOccupied ? "Occupied" : "Vacant",
                //display the tenant name or N/A if no tenant assigned
                TenantName = a.TenantId.HasValue && a.Tenant != null ? a.Tenant.Username : "N/A",
                TenantId = a.TenantId

            }).ToList();

            //Load available tenants for the dropdown (only users with the 'User' role)
            await LoadAvailableTenantsAsync();

        }

        private async Task LoadAvailableTenantsAsync()
        {
            //Fetch users with 'User' role who are not currently assigned to any apartment
            var unassignedTenants = await dbData.Users
                .Where(u => u.Role == UserRoles.User)
                //Exclude users who are already assigned to an apartment
                .Where(u => !dbData.Apartments.Any(a => a.TenantId == u.Id))
                .OrderBy(u => u.Username)
                .ToListAsync();


            //create a select list for the dropdown
            var selectListItems = unassignedTenants
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.Username
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
          
            // TenantId must be 0 or greater
            if(ApartmentInput.TenantId.HasValue && ApartmentInput.TenantId.Value > 0)
            {
                ApartmentInput.IsOccupied = true;
            }
            else
            {
                ApartmentInput.TenantId = null;
                ApartmentInput.IsOccupied = false;
            }

                dbData.Apartments.Add(ApartmentInput);
            await dbData.SaveChangesAsync();

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

            // An apartment is considered occupied if it has a valid TenantId reference
            if (apartmentToDelete.TenantId.HasValue && apartmentToDelete.TenantId.Value > 0)
            {
                ErrorMessage = $"Cannot delete unit '{apartmentToDelete.UnitNumber}' because it is currently occupied (TenantId: {apartmentToDelete.TenantId}). Please vacate the unit first.";
                return RedirectToPage();
            }

            // Safety check 1 check for associated bills
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

        //Post handler for assigning a user to an apartment unit
        public async Task<IActionResult> OnPostAssignTenantAsync(int apartmentId, int tenantId)
        {
            var apartment = await dbData.Apartments.FindAsync(apartmentId);
            var tenant = await dbData.Users.FindAsync(tenantId);

            if (apartment == null || tenant == null)
            {
                ErrorMessage = "Apartment or Tenant not found.";
                return RedirectToPage();
            }

            // check if the apartment is already occupied by a different tenant
            if (apartment.TenantId.HasValue && apartment.TenantId.Value > 0 && apartment.TenantId.Value != tenantId)
            {
                SuccessMessage = $"Apartment {apartment.UnitNumber} was previously occupied by Tenant ID {apartment.TenantId}. Tenant has been updated.";
            }
            else
            {
                SuccessMessage = $"Tenant '{tenant.Username}' assigned to apartment '{apartment.UnitNumber}' successfully.";
            }

            apartment.TenantId = tenantId;
            apartment.IsOccupied = true;

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

            apartment.TenantId = null;
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

            if (TenantId.HasValue && TenantId.Value > 0)
            {
                apartment.TenantId = TenantId.Value;
                apartment.IsOccupied = true;
            }
            else
            {
                apartment.TenantId = null;
                apartment.IsOccupied = false;
            }

            dbData.Apartments.Update(apartment);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"Apartment '{apartment.UnitNumber}' updated successfully.";
            return RedirectToPage();
        }

    }
}
