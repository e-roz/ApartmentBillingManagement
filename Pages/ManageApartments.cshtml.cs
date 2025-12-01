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

        // Separate property for tenant selection (since ApartmentModel no longer has TenantId)
        [BindProperty]
        public int? SelectedTenantId { get; set; }

        //holding the list of available apartments for the main table view
        public List<ApartmentList> Apartments { get; set; } = new List<ApartmentList>();

        //hold the list of available tenants for the dropdown
        public SelectList AvailableTenants { get; set; } = new SelectList(new List<SelectListItem>());


        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public ManageApartmentsModel(ApplicationDbContext context)
        {
            dbData = context;
        }

        // -- Core Logic for the Manage Apartments Page will go here --

        public async Task OnGetAsync()
        {
            // Get all apartments with their active leases
            var now = DateTime.UtcNow;
            var apartmentEntities = await dbData.Apartments
                .Include(a => a.Leases)
                    .ThenInclude(l => l.User)
                .OrderBy(a => a.UnitNumber)
                .ToListAsync();

            //Map to ApartmentList for display
            Apartments = apartmentEntities.Select(a =>
            {
                // Find active lease (lease end date is in the future)
                var activeLease = a.Leases.FirstOrDefault(l => l.LeaseEnd >= now);
                bool isOccupied = activeLease != null;
                
                return new ApartmentList
                {
                    Id = a.Id,
                    UnitNumber = a.UnitNumber,
                    MonthlyRent = a.MonthlyRent,
                    StatusDisplay = isOccupied ? "Occupied" : "Vacant",
                    //display the tenant name or N/A if no tenant assigned
                    TenantName = activeLease?.User != null ? (activeLease.User.Username ?? activeLease.User.Email) : "N/A",
                    TenantId = activeLease?.UserId
                };
            }).ToList();

            //Load available tenants for the dropdown
            await LoadAvailableTenantsAsync();
        }

        private async Task LoadAvailableTenantsAsync()
        {
            var now = DateTime.UtcNow;
            
            // Get all users with active leases
            var usersWithActiveLeases = await dbData.Leases
                .Where(l => l.LeaseEnd >= now)
                .Select(l => l.UserId)
                .Distinct()
                .ToListAsync();

            //Fetch users (tenants) who don't have active leases
            var unassignedUsers = await dbData.Users
                .Where(u => u.Role == UserRoles.Tenant && 
                           (u.Status == "Active" || u.Status == null) &&
                           !usersWithActiveLeases.Contains(u.Id))
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

                // Set IsOccupied based on whether there's an active tenant for this apartment
                // (This will be updated when a tenant is assigned)
                ApartmentInput.IsOccupied = false;

                dbData.Apartments.Add(ApartmentInput);
                await dbData.SaveChangesAsync();

                // If SelectedTenantId was provided, create a lease for the user (tenant)
                if (SelectedTenantId.HasValue && SelectedTenantId.Value > 0)
                {
                    var user = await dbData.Users.FindAsync(SelectedTenantId.Value);
                    if (user != null)
                    {
                        // End any existing active leases for this apartment
                        var existingLeases = await dbData.Leases
                            .Where(l => l.ApartmentId == ApartmentInput.Id && l.LeaseEnd >= DateTime.UtcNow)
                            .ToListAsync();
                        
                        foreach (var lease in existingLeases)
                        {
                            lease.LeaseEnd = DateTime.UtcNow.AddDays(-1); // End existing lease
                        }

                        // Create new lease
                        var newLease = new Lease
                        {
                            UserId = user.Id,
                            ApartmentId = ApartmentInput.Id,
                            LeaseStart = DateTime.UtcNow,
                            LeaseEnd = DateTime.UtcNow.AddYears(1),
                            MonthlyRent = ApartmentInput.MonthlyRent,
                            UnitNumber = ApartmentInput.UnitNumber
                        };

                        dbData.Leases.Add(newLease);
                        ApartmentInput.IsOccupied = true;
                        dbData.Apartments.Update(ApartmentInput);
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
