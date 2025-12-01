using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageLeasesModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public ManageLeasesModel(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // List of leases for display
        public List<LeaseViewModel> Leases { get; set; } = new List<LeaseViewModel>();

        // For dropdowns
        public SelectList AvailableTenants { get; set; } = new SelectList(new List<SelectListItem>());
        public SelectList AvailableApartments { get; set; } = new SelectList(new List<SelectListItem>());

        // For form binding
        [BindProperty]
        public LeaseInputModel LeaseInput { get; set; } = new LeaseInputModel();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadLeasesAsync();
            await LoadDropdownsAsync();
        }

        private async Task LoadLeasesAsync()
        {
            var leases = await _context.Leases
                .Include(l => l.User)
                .Include(l => l.Apartment)
                .OrderByDescending(l => l.LeaseStart)
                .ToListAsync();

            var now = DateTime.UtcNow;
            Leases = leases.Select(l => new LeaseViewModel
            {
                Id = l.Id,
                TenantName = l.User?.Username ?? l.User?.Email ?? "Unknown",
                TenantEmail = l.User?.Email ?? "N/A",
                ApartmentUnit = l.UnitNumber,
                ApartmentStatus = l.Apartment?.IsOccupied == true ? "Occupied" : "Available",
                MonthlyRent = l.MonthlyRent,
                SecurityDeposit = l.SecurityDeposit,
                LeaseStart = l.LeaseStart,
                LeaseEnd = l.LeaseEnd,
                LeaseStatus = GetLeaseStatus(l.LeaseStart, l.LeaseEnd, now),
                UserId = l.UserId,
                ApartmentId = l.ApartmentId
            }).ToList();
        }

        private string GetLeaseStatus(DateTime start, DateTime end, DateTime now)
        {
            if (now < start)
                return "Upcoming";
            if (now >= start && now <= end)
                return "Active";
            return "Expired";
        }

        private async Task LoadDropdownsAsync()
        {
            // Load tenants
            var tenants = await _context.Users
                .Where(u => u.Role == UserRoles.Tenant && (u.Status == "Active" || u.Status == null))
                .OrderBy(u => u.Username)
                .ToListAsync();

            var tenantItems = tenants.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = $"{t.Username} ({t.Email})"
            }).ToList();

            AvailableTenants = new SelectList(tenantItems, "Value", "Text");

            // Load apartments - show all apartments, but mark occupied ones
            var apartments = await _context.Apartments
                .OrderBy(a => a.UnitNumber)
                .ToListAsync();

            var apartmentItems = apartments.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.UnitNumber} - ${a.MonthlyRent:N2}/month {(a.IsOccupied ? "(Occupied)" : "")}",
                Disabled = false // Allow selection of all apartments for editing
            }).ToList();

            AvailableApartments = new SelectList(apartmentItems, "Value", "Text");
        }

        public async Task<IActionResult> OnPostCreateLeaseAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Invalid lease data. Please check all fields.";
                return Page();
            }

            // Validate dates
            if (LeaseInput.LeaseStart >= LeaseInput.LeaseEnd)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Lease end date must be after lease start date.";
                return Page();
            }

            // Check if apartment is available
            var apartment = await _context.Apartments
                .Include(a => a.Leases)
                .FirstOrDefaultAsync(a => a.Id == LeaseInput.ApartmentId);

            if (apartment == null)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Selected apartment not found.";
                return Page();
            }

            // Check for overlapping leases
            var overlappingLease = apartment.Leases.Any(l =>
                (LeaseInput.LeaseStart >= l.LeaseStart && LeaseInput.LeaseStart <= l.LeaseEnd) ||
                (LeaseInput.LeaseEnd >= l.LeaseStart && LeaseInput.LeaseEnd <= l.LeaseEnd) ||
                (LeaseInput.LeaseStart <= l.LeaseStart && LeaseInput.LeaseEnd >= l.LeaseEnd));

            if (overlappingLease)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "This apartment already has a lease during the selected period.";
                return Page();
            }

            var user = await _context.Users.FindAsync(LeaseInput.UserId);
            if (user == null)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Selected tenant not found.";
                return Page();
            }

            var newLease = new Lease
            {
                UserId = LeaseInput.UserId,
                ApartmentId = LeaseInput.ApartmentId,
                LeaseStart = LeaseInput.LeaseStart,
                LeaseEnd = LeaseInput.LeaseEnd,
                MonthlyRent = LeaseInput.MonthlyRent,
                SecurityDeposit = LeaseInput.SecurityDeposit,
                UnitNumber = apartment.UnitNumber
            };

            _context.Leases.Add(newLease);

            // Update apartment occupancy if lease is active
            var now = DateTime.UtcNow;
            if (LeaseInput.LeaseStart <= now && LeaseInput.LeaseEnd >= now)
            {
                apartment.IsOccupied = true;
                _context.Apartments.Update(apartment);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var adminUserId))
            {
                var details = $"Created new lease for {user.Username} in unit {apartment.UnitNumber}.";
                await _auditService.LogAsync(AuditActionType.CreateTenant, adminUserId, details, newLease.Id, nameof(Lease));
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Lease created successfully for {user.Username} in unit {apartment.UnitNumber}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditLeaseAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Invalid lease data. Please check all fields.";
                return Page();
            }

            var lease = await _context.Leases
                .Include(l => l.Apartment)
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == LeaseInput.Id);

            if (lease == null)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Lease not found.";
                return Page();
            }

            // Validate dates
            if (LeaseInput.LeaseStart >= LeaseInput.LeaseEnd)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Lease end date must be after lease start date.";
                return Page();
            }

            // Check for overlapping leases (excluding current lease)
            var apartment = await _context.Apartments
                .Include(a => a.Leases)
                .FirstOrDefaultAsync(a => a.Id == LeaseInput.ApartmentId);

            if (apartment == null)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Selected apartment not found.";
                return Page();
            }

            var overlappingLease = apartment.Leases.Any(l => l.Id != LeaseInput.Id &&
                ((LeaseInput.LeaseStart >= l.LeaseStart && LeaseInput.LeaseStart <= l.LeaseEnd) ||
                 (LeaseInput.LeaseEnd >= l.LeaseStart && LeaseInput.LeaseEnd <= l.LeaseEnd) ||
                 (LeaseInput.LeaseStart <= l.LeaseStart && LeaseInput.LeaseEnd >= l.LeaseEnd)));

            if (overlappingLease)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "This apartment already has a lease during the selected period.";
                return Page();
            }

            var oldUnitNumber = lease.UnitNumber;
            lease.UserId = LeaseInput.UserId;
            lease.ApartmentId = LeaseInput.ApartmentId;
            lease.LeaseStart = LeaseInput.LeaseStart;
            lease.LeaseEnd = LeaseInput.LeaseEnd;
            lease.MonthlyRent = LeaseInput.MonthlyRent;
            lease.SecurityDeposit = LeaseInput.SecurityDeposit;
            lease.UnitNumber = apartment.UnitNumber;

            // Update apartment occupancy
            var now = DateTime.UtcNow;
            if (LeaseInput.LeaseStart <= now && LeaseInput.LeaseEnd >= now)
            {
                apartment.IsOccupied = true;
                _context.Apartments.Update(apartment);
            }
            else
            {
                // Check if there are other active leases for this apartment
                var hasActiveLease = apartment.Leases.Any(l => l.Id != lease.Id &&
                    l.LeaseStart <= now && l.LeaseEnd >= now);
                if (!hasActiveLease)
                {
                    apartment.IsOccupied = false;
                    _context.Apartments.Update(apartment);
                }
            }

            _context.Leases.Update(lease);

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var adminUserId))
            {
                var details = $"Updated lease for {lease.User?.Username} in unit {apartment.UnitNumber}.";
                await _auditService.LogAsync(AuditActionType.UpdateTenant, adminUserId, details, lease.Id, nameof(Lease));
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Lease updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteLeaseAsync(int id)
        {
            var lease = await _context.Leases
                .Include(l => l.Apartment)
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lease == null)
            {
                ErrorMessage = "Lease not found.";
                return RedirectToPage();
            }

            var unitNumber = lease.UnitNumber;
            var tenantName = lease.User?.Username ?? lease.User?.Email ?? "Unknown";

            _context.Leases.Remove(lease);

            // Update apartment occupancy if this was an active lease
            var now = DateTime.UtcNow;
            if (lease.LeaseStart <= now && lease.LeaseEnd >= now)
            {
                var apartment = lease.Apartment;
                if (apartment != null)
                {
                    // Check if there are other active leases
                    var hasOtherActiveLease = await _context.Leases
                        .AnyAsync(l => l.ApartmentId == apartment.Id && 
                                      l.Id != id && 
                                      l.LeaseStart <= now && 
                                      l.LeaseEnd >= now);
                    
                    if (!hasOtherActiveLease)
                    {
                        apartment.IsOccupied = false;
                        _context.Apartments.Update(apartment);
                    }
                }
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var adminUserId))
            {
                var details = $"Deleted lease for {tenantName} in unit {unitNumber}.";
                await _auditService.LogAsync(AuditActionType.DeleteTenant, adminUserId, details, id, nameof(Lease));
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Lease for {tenantName} in unit {unitNumber} deleted successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetLeaseDetailsAsync(int id)
        {
            var lease = await _context.Leases
                .Include(l => l.User)
                .Include(l => l.Apartment)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lease == null)
            {
                return new JsonResult(new { error = "Lease not found" });
            }

            var now = DateTime.UtcNow;
            return new JsonResult(new
            {
                id = lease.Id,
                userId = lease.UserId,
                apartmentId = lease.ApartmentId,
                leaseStart = lease.LeaseStart.ToString("yyyy-MM-dd"),
                leaseEnd = lease.LeaseEnd.ToString("yyyy-MM-dd"),
                monthlyRent = lease.MonthlyRent,
                securityDeposit = lease.SecurityDeposit,
                status = GetLeaseStatus(lease.LeaseStart, lease.LeaseEnd, now)
            });
        }
    }

    // ViewModel for displaying leases
    public class LeaseViewModel
    {
        public int Id { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantEmail { get; set; } = string.Empty;
        public string ApartmentUnit { get; set; } = string.Empty;
        public string ApartmentStatus { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public decimal SecurityDeposit { get; set; }
        public DateTime LeaseStart { get; set; }
        public DateTime LeaseEnd { get; set; }
        public string LeaseStatus { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int ApartmentId { get; set; }
    }

    // Input model for form binding
    public class LeaseInputModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ApartmentId { get; set; }
        public DateTime LeaseStart { get; set; } = DateTime.UtcNow;
        public DateTime LeaseEnd { get; set; } = DateTime.UtcNow.AddYears(1);
        public decimal MonthlyRent { get; set; }
        public decimal SecurityDeposit { get; set; }
    }
}

