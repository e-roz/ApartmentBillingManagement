using Apartment.Data;
using Apartment.Model;
using Apartment.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class ProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ProfileModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ProfileInputModel Input { get; set; } = new();

        [BindProperty]
        public PasswordChangeModel PasswordInput { get; set; } = new();

        public Model.User? UserInfo { get; set; }

        public class ProfileInputModel
        {
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
        }

        public class PasswordChangeModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [MinLength(6)]
            public string NewPassword { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Compare("NewPassword")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadUserDataAsync();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadUserDataAsync();
                return Page();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    if (!string.IsNullOrWhiteSpace(Input.Email))
                    {
                        user.Email = Input.Email;
                    }

                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Profile updated successfully.";
                    return RedirectToPage();
                }
            }

            ModelState.AddModelError("", "Unable to update profile. Please try again.");
            await LoadUserDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadUserDataAsync();
                return Page();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    // Verify current password
                    if (!PasswordHasher.VerifyPassword(PasswordInput.CurrentPassword, user.HasedPassword))
                    {
                        ModelState.AddModelError("PasswordInput.CurrentPassword", "Current password is incorrect.");
                        await LoadUserDataAsync();
                        return Page();
                    }

                    // Update password
                    user.HasedPassword = PasswordHasher.HashPassword(PasswordInput.NewPassword);
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Password changed successfully.";
                    return RedirectToPage();
                }
            }

            ModelState.AddModelError("", "Unable to change password. Please try again.");
            await LoadUserDataAsync();
            return Page();
        }

        private async Task LoadUserDataAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    UserInfo = user;
                    Input.Email = user.Email;
                }
            }
        }
    }
}

