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
        [BindProperty]
        public ApartmentModel ApartmentInput { get; set; } = new ApartmentModel();

        // Separate property for tenant selection (since ApartmentModel no longer has TenantId)
        [BindProperty]
        public int? SelectedTenantId { get; set; }

        //property to hold the search term
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }


        //holding the list of available apartments for the main table view
        public List<ViewModels.ApartmentList> Apartments { get; set; } = new List<ViewModels.ApartmentList>();

        //hold the list of available tenants for the dropdown
        public SelectList AvailableTenants { get; set; } = new SelectList(new List<SelectListItem>());


        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public ManageApartmentsModel(ApplicationDbContext context, IAuditService auditService)
        {
            dbData = context;
            _auditService = auditService;
        }

        private async Task CreateLeaseAsync(User user, ApartmentModel apartment, DateTime? leaseStart = null, DateTime? leaseEnd = null)
        {
            // End any existing active leases for this apartment
            var existingLeases = await dbData.Leases
                .Where(l => l.ApartmentId == apartment.Id && l.LeaseEnd >= DateTime.UtcNow)
                .ToListAsync();
            
            foreach (var lease in existingLeases)
            {
                lease.LeaseEnd = DateTime.UtcNow.AddDays(-1); // End existing lease
            }

            // Create new lease
            var newLease = new Lease
            {
                UserId = user.Id,
                ApartmentId = apartment.Id,
                LeaseStart = leaseStart ?? DateTime.UtcNow,
                LeaseEnd = leaseEnd ?? DateTime.UtcNow.AddYears(1),
                MonthlyRent = apartment.MonthlyRent,
                UnitNumber = apartment.UnitNumber
            };

            dbData.Leases.Add(newLease);
            apartment.IsOccupied = true;
            dbData.Apartments.Update(apartment);
        }

        private async Task EndLeaseAsync(int apartmentId)
        {
            // End all active leases for this apartment
            var activeLeases = await dbData.Leases
                .Where(l => l.ApartmentId == apartmentId && l.LeaseEnd >= DateTime.UtcNow)
                .ToListAsync();
            
            foreach (var lease in activeLeases)
            {
                lease.LeaseEnd = DateTime.UtcNow.AddDays(-1);
            }

            var apartment = await dbData.Apartments.FindAsync(apartmentId);
            if (apartment != null)
            {
                apartment.IsOccupied = false;
                dbData.Apartments.Update(apartment);
            }
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
                    MonthlyRent = a.MonthlyRent,
                    StatusDisplay = isOccupied ? "Occupied" : "Vacant",
                    TenantName = activeLease?.User != null ? (activeLease.User.Username ?? activeLease.User.Email) : "N/A",
                    TenantId = activeLease?.UserId
                };
            }).ToList();
            
            // Save any IsOccupied flag updates
            await dbData.SaveChangesAsync();

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

            //create a select list for the dropdown
            var selectListItems = unassignedUsers
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.Username ?? u.Email
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
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                ErrorMessage = "Could not identify the administrator performing the action.";
                return RedirectToPage();
            }
            var details = $"Created new apartment/unit: {ApartmentInput.UnitNumber}.";
            await _auditService.LogAsync(AuditActionType.CreateApartment, userId, details, ApartmentInput.Id, nameof(ApartmentModel));
            
            await dbData.SaveChangesAsync();

            // If SelectedTenantId was provided, create a lease for the user (tenant)
            if (SelectedTenantId.HasValue && SelectedTenantId.Value > 0)
            {
                var user = await dbData.Users.FindAsync(SelectedTenantId.Value);
                if (user != null)
                {
                    await CreateLeaseAsync(user, ApartmentInput);
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

        //Post handler for assigning a user (tenant) to an apartment unit
        public async Task<IActionResult> OnPostAssignTenantAsync(int apartmentId, int tenantId)
        {
            var apartment = await dbData.Apartments.FindAsync(apartmentId);
            var user = await dbData.Users.FindAsync(tenantId);

            if (apartment == null || user == null)
            {
                ErrorMessage = "Apartment or User not found.";
                return RedirectToPage();
            }

            var now = DateTime.UtcNow;
            // Check if the apartment is already occupied by a different active lease
            var existingLease = await dbData.Leases
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.ApartmentId == apartmentId && l.LeaseEnd >= now && l.UserId != tenantId);

            if (existingLease != null)
            {
                // End the previous lease
                existingLease.LeaseEnd = now.AddDays(-1);
                SuccessMessage = $"Apartment {apartment.UnitNumber} was previously occupied by {existingLease.User.Username ?? existingLease.User.Email}. Lease has been ended.";
            }
            else
            {
                SuccessMessage = $"User '{user.Username ?? user.Email}' assigned to apartment '{apartment.UnitNumber}' successfully.";
            }

            // Create new lease for the user
            await CreateLeaseAsync(user, apartment);
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

            // End all active leases for this apartment
            await EndLeaseAsync(id);
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

            // Handle user (tenant) assignment/unassignment via leases
            var now = DateTime.UtcNow;
            if (TenantId.HasValue && TenantId.Value > 0)
            {
                var user = await dbData.Users.FindAsync(TenantId.Value);
                if (user != null)
                {
                    // End any existing active leases for this apartment (except for the same user)
                    var existingLeases = await dbData.Leases
                        .Where(l => l.ApartmentId == ApartmentId && l.LeaseEnd >= now && l.UserId != TenantId.Value)
                        .ToListAsync();
                    
                    foreach (var lease in existingLeases)
                    {
                        lease.LeaseEnd = now.AddDays(-1);
                    }

                    // Create new lease for the user
                    await CreateLeaseAsync(user, apartment);
                }
                else
                {
                    await EndLeaseAsync(ApartmentId);
                }
            }
            else
            {
                // End all active leases for this apartment
                await EndLeaseAsync(ApartmentId);
            }

            dbData.Apartments.Update(apartment);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"Apartment '{apartment.UnitNumber}' updated successfully.";
            return RedirectToPage();
        }

    }
}
