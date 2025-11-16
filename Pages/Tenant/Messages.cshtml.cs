using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class MessagesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MessagesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Model.Tenant? TenantInfo { get; set; }

        public class MessageViewModel
        {
            public int Id { get; set; }
            public string Subject { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime SentDate { get; set; }
            public bool IsRead { get; set; }
        }

        public List<MessageViewModel> Messages { get; set; } = new();

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

            // In a real implementation, this would query a Messages table
            // For now, we'll show mock data
            Messages = new List<MessageViewModel>
            {
                new MessageViewModel
                {
                    Id = 1,
                    Subject = "Welcome to Your New Home!",
                    Content = "We're excited to have you as a resident. If you have any questions, please don't hesitate to contact us.",
                    SentDate = DateTime.Now.AddDays(-5),
                    IsRead = true
                },
                new MessageViewModel
                {
                    Id = 2,
                    Subject = "Building Maintenance Notice",
                    Content = "Scheduled maintenance will occur on the elevators this Saturday from 8 AM to 12 PM. We apologize for any inconvenience.",
                    SentDate = DateTime.Now.AddDays(-2),
                    IsRead = false
                }
            };
        }
    }
}

