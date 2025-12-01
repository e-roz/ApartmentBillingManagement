using System.Security.Claims;
using System.Threading.Tasks;
using Apartment.Data;
using Apartment.Services;
using Apartment.Utilities;
using Apartment.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Pages
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        [BindProperty]
        public ChangePasswordViewModel Input { get; set; } = new();

        [TempData]
        public string? ErrorMessage { get; set; }
        
        [TempData]
        public string? SuccessMessage { get; set; }

        public ChangePasswordModel(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FindAsync(userId);

            if (user == null || !user.MustChangePassword)
            {
                // If they don't need to change password, send them to their dashboard.
                return RedirectToPage(user.Role == Enums.UserRoles.Admin ? "/Admin/AdminDashboard" : "/TenantDashboard");
            }
            
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                ModelState.AddModelError("", "Unable to identify user. Please log in again.");
                return Page();
            }

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                // This should not happen for an authenticated user
                return RedirectToPage("/Login");
            }

            // Hash the new password and update the user
            user.HasedPassword = PasswordHasher.HashPassword(Input.NewPassword);
            user.MustChangePassword = false;
            user.UpdatedAt = System.DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Log the password change
            await _auditService.LogAsync(
                action: Enums.AuditActionType.UpdateUser,
                userId: user.Id,
                details: "User changed their temporary password after first login.",
                entityId: user.Id,
                entityType: "User"
            );
            
            SuccessMessage = "Your password has been changed successfully. Please log in again.";

            // Sign the user out and redirect to login page
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Login");
        }
    }
}
