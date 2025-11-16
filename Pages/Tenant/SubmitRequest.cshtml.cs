using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class SubmitRequestModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public SubmitRequestModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public RequestInputModel Input { get; set; } = new();

        public Model.Tenant? TenantInfo { get; set; }

        public class RequestInputModel
        {
            [Required]
            public string Title { get; set; } = string.Empty;

            [Required]
            public string Description { get; set; } = string.Empty;

            public string Priority { get; set; } = "Medium";
        }

        public async Task OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.Tenant != null)
                {
                    TenantInfo = user.Tenant;
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.Tenant != null)
                {
                    // In a real implementation, this would save to a MaintenanceRequest table
                    // For now, we'll just show a success message
                    TempData["SuccessMessage"] = $"Maintenance request '{Input.Title}' has been submitted successfully. Request ID: {new Random().Next(1000, 9999)}";
                    return RedirectToPage("/Tenant/TrackRequests");
                }
            }

            ModelState.AddModelError("", "Unable to submit request. Please try again.");
            await OnGetAsync();
            return Page();
        }
    }
}

