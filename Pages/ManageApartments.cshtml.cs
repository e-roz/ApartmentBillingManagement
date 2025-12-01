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
                    StatusDisplay = isOccupied ? "Occupied" : "Vacant",
                    //display the tenant name or N/A if no tenant assigned
                    TenantName = activeLease?.User != null ? (activeLease.User.Username ?? activeLease.User.Email) : "N/A",
                    TenantId = activeLease?.UserId
                };
            }).ToList();

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

                // Set IsOccupied initially to false; it will be updated when a lease is created on the ManageLeases page.
                ApartmentInput.IsOccupied = false;

                dbData.Apartments.Add(ApartmentInput);
                await dbData.SaveChangesAsync();

                SuccessMessage = $"Apartment unit '{ApartmentInput.UnitNumber}' added successfully. You can now create a lease for this unit from the Lease Management page.";
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
