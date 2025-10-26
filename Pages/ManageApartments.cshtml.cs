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
            // fetch all apartments, including their tenants if available
            var apartmentEntities = await dbData.Apartments
                .Include(a => a.Tenant)
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
            // Repopulate the tenants dropdown in case of error
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Invalid apartment data. Please check the inputs.";
                await OnGetAsync();
                return Page();
            }

            try
            {
                if (await dbData.Apartments.AnyAsync(a => a.UnitNumber == ApartmentInput.UnitNumber))
                {
                    ErrorMessage = $"Unit number '{ApartmentInput.UnitNumber}' already exists.";
                    await OnGetAsync();
                    return Page();
                }

                //IMPORTANT: When adding a new apartment, it should be vacant by default
                //tenant will be 0 if vacant was selected
                if (ApartmentInput.TenantId == 0)
                {
                    ApartmentInput.TenantId = null;
                    ApartmentInput.IsOccupied = false;
                }
                else
                {
                    ApartmentInput.IsOccupied = true;
                }
                dbData.Apartments.Add(ApartmentInput);
                await dbData.SaveChangesAsync();

                SuccessMessage = $"Apartment unit '{ApartmentInput.UnitNumber}' added successfully.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                await OnGetAsync();
                ErrorMessage = $"Error adding apartment: {ex.Message}";
                return Page();
            }
        }

        //EDIT APARTMENT LOGIC
        public async Task<IActionResult> OnPostEditApartmentAsync(int ApartmentId, string UnitNumber, decimal MonthlyRent, int? TenantId)
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Validation failed. Please check the form.";
                await OnGetAsync();
                return Page();
            }

            var apartmentToUpdate = await dbData.Apartments.FindAsync(ApartmentId);

            if (apartmentToUpdate == null)
            {
                ErrorMessage = "Apartment not found.";
                return RedirectToPage();
            }

            try
            {
                // Check for unique unit number against other apartments
                if (await dbData.Apartments.AnyAsync(a => a.UnitNumber == UnitNumber && a.Id != ApartmentId))
                {
                    ErrorMessage = $"Unit number '{UnitNumber}' already exists.";
                    await OnGetAsync();
                    return Page();
                }

                // Update properties
                apartmentToUpdate.UnitNumber = UnitNumber;
                apartmentToUpdate.MonthlyRent = MonthlyRent;

                // IMPORTANT: Handle tenant assignment and occupancy status
                // If TenantId is 0, it means "Vacant"
                if (TenantId == 0)
                {
                    apartmentToUpdate.TenantId = null;
                    apartmentToUpdate.IsOccupied = false;
                }
                else if (TenantId.HasValue)
                {
                    apartmentToUpdate.TenantId = TenantId.Value;
                    apartmentToUpdate.IsOccupied = true;
                }

                dbData.Apartments.Update(apartmentToUpdate);
                await dbData.SaveChangesAsync();

                SuccessMessage = $"Apartment unit '{apartmentToUpdate.UnitNumber}' updated successfully.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                await OnGetAsync();
                ErrorMessage = $"Error updating apartment: {ex.Message}";
                return Page();
            }
        }
        public async Task<IActionResult> OnPostDeleteApartmentAsync(int ApartmentId)
        {
            // Include Bills to perform the safety check
            var apartmentToDelete = await dbData.Apartments
                 .Include(a => a.Bills)
                 .FirstOrDefaultAsync(a => a.Id == ApartmentId);

            if (apartmentToDelete == null)
            {
                ErrorMessage = "Apartment not found.";
                return RedirectToPage();
            }
            // Safety check: Prevent deletion if there are associated bills
            if (apartmentToDelete.Bills.Any())
            {
                ErrorMessage = $"Cannot delete unit '{apartmentToDelete.UnitNumber}' because {apartmentToDelete.Bills.Count} bill(s) are associated with it. Please resolve or delete the bills first.";
                return RedirectToPage();
            }
            try
            {
                dbData.Apartments.Remove(apartmentToDelete);
                await dbData.SaveChangesAsync();

                SuccessMessage = $"Apartment unit '{apartmentToDelete.UnitNumber}' deleted successfully.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting apartment: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
