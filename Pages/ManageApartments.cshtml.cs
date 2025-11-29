using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Pages
{

    [Authorize(Roles = "Admin")]
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
                .Include(a => a.CurrentTenant) // CurrentTenant is now a User
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
                TenantName = a.CurrentTenant != null ? a.CurrentTenant.Username : "N/A",
                TenantId = a.CurrentTenant != null ? a.CurrentTenant.Id : (int?)null

            }).ToList();

            //Load available tenants for the dropdown (only users with the 'User' role)
            await LoadAvailableTenantsAsync();

        }

        private async Task LoadAvailableTenantsAsync()
        {
            // Fetch users (tenants) who are not currently assigned to any apartment
            var unassignedUsers = await dbData.Users
                .Where(u => u.Role == UserRoles.User && u.ApartmentId == null) // Only User role, not assigned to apartment
                .OrderBy(u => u.Username)
                .ToListAsync();


            // Create a select list for the dropdown
            var selectListItems = unassignedUsers
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.Username ?? u.Email // Use Username, fallback to Email
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

                // If a tenant was assigned, update the user's ApartmentId
                if (ApartmentInput.TenantId.HasValue && ApartmentInput.TenantId.Value != 0)
                {
                    var assignedUser = await dbData.Users.FindAsync(ApartmentInput.TenantId.Value);
                    if (assignedUser != null)
                    {
                        assignedUser.ApartmentId = ApartmentInput.Id;
                        dbData.Users.Update(assignedUser);
                        await dbData.SaveChangesAsync();
                    }
                }

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
    }
}
