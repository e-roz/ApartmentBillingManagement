using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace Apartment.Pages.Admin
{

    [Authorize(Roles = "Admin")]
    public class ManageApartmentsModel : PageModel
    {
        private readonly ApplicationDbContext dbData;
        private readonly IAuditService _auditService;


        // BindProperty for the form data (Used for Add/Edit)
        // Note: Apartments are now created WITHOUT monthly rent or tenant assignment.
        // Monthly rent and tenant are handled via leases on the ManageLeases page.
        [BindProperty]
        public ApartmentModel ApartmentInput { get; set; } = new ApartmentModel();

        //property to hold the search term
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }


        //holding the list of available apartments for the main table view
        public List<ViewModels.ApartmentList> Apartments { get; set; } = new List<ViewModels.ApartmentList>();

        public SelectList ApartmentTypes { get; set; }


        [TempData]
        public string? SuccessMessage { get; set; }


        [TempData]
        public string? ErrorMessage { get; set; }

        public ManageApartmentsModel(ApplicationDbContext context, IAuditService auditService)
        {
            dbData = context;
            _auditService = auditService;
        }

        // -- Core Logic for the Manage Apartments Page will go here --

        public async Task OnGetAsync()
        {
            // Get all apartments with their active leases
            var apartmentQuery = dbData.Apartments
                .Include(a => a.Leases)
                    .ThenInclude(l => l.User)
                .AsQueryable();

            // search logic filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                string term = SearchTerm.Trim();
                apartmentQuery = apartmentQuery.Where(a =>
                    a.UnitNumber.Contains(term) ||
                    a.Leases.Any(l => l.LeaseEnd >= DateTime.UtcNow &&
                                     (l.User.Username.Contains(term) || l.User.Email.Contains(term)))
                );
            }

            // execute composed query
            var apartmentEntities = await apartmentQuery
                .OrderBy(a => a.UnitNumber)
                .ToListAsync();

            // Get current date for lease checking
            var now = DateTime.UtcNow;

            //Map to ApartmentList for display and sync IsOccupied flag
            Apartments = apartmentEntities.Select(a =>
            {
                // Find active lease (lease end date is in the future)
                var activeLease = a.Leases.FirstOrDefault(l => l.LeaseEnd >= now);
                bool isOccupied = activeLease != null;

                // Sync IsOccupied flag with actual lease status
                if (a.IsOccupied != isOccupied)
                {
                    a.IsOccupied = isOccupied;
                    dbData.Apartments.Update(a);
                }

                return new ViewModels.ApartmentList
                {
                    Id = a.Id,
                    UnitNumber = a.UnitNumber,
                    StatusDisplay = isOccupied ? "Occupied" : "Vacant",
                    TenantName = activeLease?.User != null ? (activeLease.User.Username ?? activeLease.User.Email) : "N/A",
                    TenantId = activeLease?.UserId,
                    ApartmentType = a.ApartmentType
                };
            }).ToList();

            // Save any IsOccupied flag updates
            await dbData.SaveChangesAsync();

            ApartmentTypes = new SelectList(Enum.GetValues(typeof(ApartmentType)));
        }


        


        //ADD APARTMENT LOGIC
        public async Task<IActionResult> OnPostAddApartmentAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync(); //Reload data for the page
                ErrorMessage = "Invalid apartment data. Please check the inputs.";
                return Page();
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
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                ErrorMessage = "Could not identify the administrator performing the action.";
                return RedirectToPage();
            }
            var details = $"Created new apartment/unit: {ApartmentInput.UnitNumber}.";
            await _auditService.LogAsync(AuditActionType.CreateApartment, userId, details, ApartmentInput.Id, nameof(ApartmentModel));
            
            await dbData.SaveChangesAsync();

            SuccessMessage = $"Apartment '{ApartmentInput.UnitNumber}' added successfully. You can now create a lease for this unit from the Lease Management page.";
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

            // Check if apartment has active leases
            var now = DateTime.UtcNow;
            var activeLease = await dbData.Leases
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.ApartmentId == ApartmentId && l.LeaseEnd >= now);

            if (activeLease != null)
            {
                ErrorMessage = $"Cannot delete unit '{apartmentToDelete.UnitNumber}' because it is currently occupied by {activeLease.User.Username ?? activeLease.User.Email}. Please vacate the unit first.";
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
                var apartmentUnitNumber = apartmentToDelete.UnitNumber;
                dbData.Apartments.Remove(apartmentToDelete);
                
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdStr, out var userId))
                {
                    ErrorMessage = "Could not identify the administrator performing the action.";
                    return RedirectToPage();
                }
                var details = $"Deleted apartment/unit: {apartmentUnitNumber}.";
                await _auditService.LogAsync(AuditActionType.DeleteApartment, userId, details, ApartmentId, nameof(ApartmentModel));
                
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

        // EDIT APARTMENT LOGIC
        public async Task<IActionResult> OnPostEditApartmentAsync(int ApartmentId, string UnitNumber)
        {
            if (string.IsNullOrWhiteSpace(UnitNumber))
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

            dbData.Apartments.Update(apartment);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"Apartment '{apartment.UnitNumber}' updated successfully.";
            return RedirectToPage();
        }

    }
}
