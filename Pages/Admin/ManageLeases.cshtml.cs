using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Apartment.Services;
using Apartment.Utilities;
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
        private readonly LeasePdfService _leasePdfService; // Inject LeasePdfService

        public ManageLeasesModel(ApplicationDbContext context, IAuditService auditService, LeasePdfService leasePdfService)
        {
            _context = context;
            _auditService = auditService;
            _leasePdfService = leasePdfService; // Initialize LeasePdfService
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

        public async Task<IActionResult> OnPostExportLeasesPdfAsync([FromForm] List<int> selectedLeaseIds)
        {
            if (selectedLeaseIds == null || !selectedLeaseIds.Any())
            {
                ErrorMessage = "No leases selected for PDF export.";
                return RedirectToPage();
            }

            var leasesToExport = await _context.Leases
                .Where(l => selectedLeaseIds.Contains(l.Id))
                .Include(l => l.User)
                .Include(l => l.Apartment)
                .ToListAsync();

            if (!leasesToExport.Any())
            {
                ErrorMessage = "Selected leases not found.";
                return RedirectToPage();
            }

            try
            {
                var pdfBytes = _leasePdfService.GenerateLeasesPdf(leasesToExport);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    ErrorMessage = "Failed to generate PDF for selected leases.";
                    return RedirectToPage();
                }

                var fileName = $"LeaseSummary_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                // Log the exception (using _auditService or ILogger)
                ErrorMessage = $"An error occurred during PDF generation: {ex.Message}";
                return RedirectToPage();
            }
        }
    
    // add an empty line here to make the change more readable
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
                LateFeeAmount = l.LateFeeAmount,
                LateFeeDays = l.LateFeeDays,
                PetsAllowed = l.PetsAllowed,
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

            // Load apartments - show all apartments, but mark occupied ones.
            // Monthly rent is now a lease-level field, so we no longer display apartment-level rent here.
            var apartments = await _context.Apartments
                .OrderBy(a => a.UnitNumber)
                .ToListAsync();

            var apartmentItems = apartments.Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.UnitNumber} {(a.IsOccupied ? "(Occupied)" : "(Vacant)")}", 
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

            // Check if apartment exists
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

            // Enforce that the apartment is currently vacant (no active lease "right now")
            var now = DateTime.UtcNow;
            var currentlyActiveLease = apartment.Leases.Any(l =>
                l.LeaseStart <= now && l.LeaseEnd >= now);
            if (currentlyActiveLease)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = "Selected apartment is currently occupied. Please choose a vacant unit.";
                return Page();
            }

            // Check for overlapping leases within the requested lease period
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
                LateFeeAmount = LeaseInput.LateFeeAmount,
                LateFeeDays = LeaseInput.LateFeeDays,
                PetsAllowed = LeaseInput.PetsAllowed,
                UnitNumber = apartment.UnitNumber
            };

            _context.Leases.Add(newLease);

            // Update apartment occupancy if lease is active
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

            // Check if lease has expired - leases cannot be edited until expiration
            var now = DateTime.UtcNow;
            if (lease.LeaseEnd >= now)
            {
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                ErrorMessage = $"This lease cannot be edited until it expires on {lease.LeaseEnd:MMM dd, yyyy}. Only expired leases can be modified.";
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

            // Note: We don't need to check 'now' again here since we already validated expiration above
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
            lease.LateFeeAmount = LeaseInput.LateFeeAmount;
            lease.LateFeeDays = LeaseInput.LateFeeDays;
            lease.PetsAllowed = LeaseInput.PetsAllowed;
            lease.UnitNumber = apartment.UnitNumber;

            // Update apartment occupancy
            // Note: 'now' is already defined above, but we'll use it here for consistency
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

        public async Task<IActionResult> OnPostDeleteLeaseAsync(int id, string adminPassword)
        {
            // Validate password is provided
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                ErrorMessage = "Password confirmation is required to delete a lease.";
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                return Page();
            }

            // Get the current admin user
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int adminUserId))
            {
                ErrorMessage = "Unable to identify the administrator. Please log in again.";
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                return Page();
            }

            var adminUser = await _context.Users.FindAsync(adminUserId);
            if (adminUser == null)
            {
                ErrorMessage = "Admin user not found.";
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                return Page();
            }

            // Verify admin password
            if (!PasswordHasher.VerifyPassword(adminPassword, adminUser.HasedPassword))
            {
                ErrorMessage = "Invalid password. Please enter your correct admin password to confirm deletion.";
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                return Page();
            }

            var lease = await _context.Leases
                .Include(l => l.Apartment)
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lease == null)
            {
                ErrorMessage = "Lease not found.";
                await LoadLeasesAsync();
                await LoadDropdownsAsync();
                return Page();
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

            // Note: adminUserId is already defined above from password verification
            var details = $"Deleted lease for {tenantName} in unit {unitNumber}.";
            await _auditService.LogAsync(AuditActionType.DeleteTenant, adminUserId, details, id, nameof(Lease));

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
            var isExpired = lease.LeaseEnd < now;
            return new JsonResult(new
            {
                id = lease.Id,
                userId = lease.UserId,
                apartmentId = lease.ApartmentId,
                leaseStart = lease.LeaseStart.ToString("yyyy-MM-dd"),
                leaseEnd = lease.LeaseEnd.ToString("yyyy-MM-dd"),
                monthlyRent = lease.MonthlyRent,
                securityDeposit = lease.SecurityDeposit,
                lateFeeAmount = lease.LateFeeAmount,
                lateFeeDays = lease.LateFeeDays,
                petsAllowed = lease.PetsAllowed,
                status = GetLeaseStatus(lease.LeaseStart, lease.LeaseEnd, now),
                isExpired = isExpired,
                expirationDate = lease.LeaseEnd.ToString("MMM dd, yyyy")
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
        public decimal LateFeeAmount { get; set; }
        public int LateFeeDays { get; set; }
        public bool PetsAllowed { get; set; }
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
        public decimal LateFeeAmount { get; set; }
        public int LateFeeDays { get; set; }
        public bool PetsAllowed { get; set; } = true;
    }
}

