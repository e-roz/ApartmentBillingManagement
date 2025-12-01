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
    [Authorize(Roles = "Tenant")]
    public class SubmitRequestModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public SubmitRequestModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public RequestInputModel Input { get; set; } = new();

        public Model.User? UserInfo { get; set; }

        public class RequestInputModel
        {
            [Required]
            public string Title { get; set; } = string.Empty;

            [Required]
            public string Description { get; set; } = string.Empty;

            [Required]
            public Apartment.Enums.RequestType RequestType { get; set; }

            [Required]
            public Apartment.Enums.RequestPriority Priority { get; set; } = Apartment.Enums.RequestPriority.Medium;
        }

        public async Task OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Leases)
                        .ThenInclude(l => l.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    UserInfo = user;
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync(); // Repopulate TenantInfo if model state is invalid
                return Page();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                ModelState.AddModelError("", "Unable to identify user. Please log in again.");
                await OnGetAsync();
                return Page();
            }

            var user = await _context.Users
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                ModelState.AddModelError("", "User account not found.");
                await OnGetAsync();
                return Page();
            }

            var newRequest = new Request
            {
                Title = Input.Title,
                Description = Input.Description,
                RequestType = Input.RequestType,
                Priority = Input.Priority,
                Status = Enums.RequestStatus.Submitted,
                DateSubmitted = DateTime.UtcNow,
                SubmittedByUserId = user.Id,
                ApartmentId = user.Leases?.FirstOrDefault(l => l.LeaseEnd >= DateTime.UtcNow)?.ApartmentId
            };

            try
            {
                _context.Requests.Add(newRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Your request '{Input.Title}' has been submitted successfully!";
                return RedirectToPage("/Tenant/ViewRequests");
            }
            catch (Exception)
            {
                // In a real application, you would log this exception
                ModelState.AddModelError("", "An unexpected error occurred while saving your request. Please try again.");
                await OnGetAsync();
                return Page();
            }
        }
    }
}

