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

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // ========== INDEX (List View) - Read-only ==========
        public async Task OnGetAsync()
        {
            // Show all Users with role Tenant (read-only view)
            Tenants = await _context.Users
                .AsNoTracking()
                .Include(u => u.Apartment)
                .Where(u => u.Role == UserRoles.Tenant)
                .OrderBy(u => u.Username)
                .Select(u => new TenantListViewModel
                {
                    Id = u.Id,
                    FullName = u.Username,
                    PrimaryEmail = u.Email,
                    PrimaryPhone = "N/A", // User model doesn't have phone
                    MonthlyRent = u.MonthlyRent ?? 0m,
                    LeaseStatus = u.Status ?? "Unknown",
                    AssignedUnit = u.UnitNumber ?? "Unassigned"
                })
                .ToListAsync();
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
                .FirstOrDefaultAsync(m => m.Id == id.Value && m.Role == UserRoles.Tenant);

            if (UserDetails == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
