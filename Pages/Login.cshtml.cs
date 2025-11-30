using Apartment.Services;
using System.Security.Claims;
using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Apartment.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Apartment.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext dbData;
        private readonly IAuditService _auditService;

        [BindProperty]
        public Login Input { get; set; } = new Login();

        [TempData]
        public string? Message { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public LoginModel(ApplicationDbContext context, IAuditService auditService)
        {
            dbData = context;
            _auditService = auditService;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await dbData.Users.FirstOrDefaultAsync(u => u.Email == Input.Email);

            if (user == null || !PasswordHasher.VerifyPassword(Input.Password, user.HasedPassword))
            {
                ErrorMessage = "Invalid email or password.";
                // Log failed login attempt. Find user by email to get their ID if they exist.
                var attemptedUser = user ?? await dbData.Users.FirstOrDefaultAsync(u => u.Email == Input.Email);
                await _auditService.LogAsync(
                    AuditActionType.UserLoginFailure,
                    attemptedUser?.Id,
                    $"Failed login attempt for email: {Input.Email}.",
                    attemptedUser?.Id,
                    nameof(User),
                    success: false
                );
                await dbData.SaveChangesAsync();
                return Page();
            }

            // Check if user account is inactive
            if (user.Status != null && user.Status.ToLower() == "inactive")
            {
                ErrorMessage = "Your account has been deactivated by the system. Please contact the administrator for more information.";
                // Log blocked login attempt due to inactive account
                await _auditService.LogAsync(
                    AuditActionType.UserLoginFailure,
                    user.Id,
                    $"Login attempt blocked for inactive account: {Input.Email}.",
                    user.Id,
                    nameof(User),
                    success: false
                );
                await dbData.SaveChangesAsync();
                return Page();
            }

            // Log successful login
            await _auditService.LogAsync(
                AuditActionType.UserLoginSuccess,
                user.Id,
                $"User '{user.Username}' logged in successfully.",
                user.Id,
                nameof(User)
            );
            await dbData.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = System.DateTimeOffset.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // After signing in, check if the user must change their password
            if (user.MustChangePassword)
            {
                return RedirectToPage("/ChangePassword");
            }

            // Redirect users to their role-specific dashboards
            if (user.Role == UserRoles.Admin)
            {
                return RedirectToPage("/Admin/AdminDashboard");
            }
            // Manager role removed - all Manager users should be migrated to Admin
            // else if (user.Role == UserRoles.Manager) - Obsolete
            else
            {
                return RedirectToPage("/TenantDashboard");
            }
        }
    }
}
