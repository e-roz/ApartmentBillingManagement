using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageTenantsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ManageTenantsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Properties for Index (List View) - Read-only
        public IList<TenantListViewModel> Tenants { get; private set; } = new List<TenantListViewModel>();

        // Properties for Details - Read-only
        public Model.User? UserDetails { get; private set; }
        public Lease? ActiveLease { get; private set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // ========== INDEX (List View) - Read-only ==========
        public async Task OnGetAsync()
        {
            var now = DateTime.UtcNow;
            // Show all Users with role Tenant (read-only view)
            var users = await _context.Users
                .AsNoTracking()
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .Where(u => u.Role == UserRoles.Tenant)
                .OrderBy(u => u.Username)
                .ToListAsync();

            Tenants = users.Select(u =>
            {
                var activeLease = u.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);
                return new TenantListViewModel
                {
                    Id = u.Id,
                    FullName = u.Username,
                    PrimaryEmail = u.Email,
                    PrimaryPhone = "N/A", // User model doesn't have phone
                    MonthlyRent = activeLease?.MonthlyRent ?? 0m,
                    LeaseStatus = u.Status ?? "Unknown",
                    AssignedUnit = activeLease?.UnitNumber ?? "Unassigned"
                };
            }).ToList();
        }

        // ========== DETAILS (Single View) - Read-only ==========
        public async Task<IActionResult> OnGetDetailsAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            UserDetails = await _context.Users
                .AsNoTracking()
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .FirstOrDefaultAsync(m => m.Id == id.Value && m.Role == UserRoles.Tenant);

            if (UserDetails == null)
            {
                return NotFound();
            }

            var now = DateTime.UtcNow;
            ActiveLease = UserDetails.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);

            return Page();
        }
    }
}
