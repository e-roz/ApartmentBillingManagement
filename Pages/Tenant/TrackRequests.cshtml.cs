using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class TrackRequestsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TrackRequestsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<RequestViewModel> Requests { get; set; } = new();
        public Model.Tenant? TenantInfo { get; set; }

        public class RequestViewModel
        {
            public int RequestId { get; set; }
            public DateTime DateSubmitted { get; set; }
            public string DescriptionSnippet { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime? DateCompleted { get; set; }
            public string Title { get; set; } = string.Empty;
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

                    // In a real implementation, this would query a MaintenanceRequest table
                    // For now, we'll show mock data or empty list
                    // This can be extended when the MaintenanceRequest entity is created
                    Requests = new List<RequestViewModel>();
                }
            }
        }
    }
}

