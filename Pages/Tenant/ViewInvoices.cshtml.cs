using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class ViewInvoicesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewInvoicesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Invoice> Invoices { get; set; } = new List<Invoice>();
        public Model.User? UserInfo { get; set; }

        public async Task OnGetAsync()
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
                    
                    // Fetch all Invoice entities where TenantUserId matches the logged-in user's ID
                    Invoices = await _context.Invoices
                        .Include(i => i.Bill!)
                            .ThenInclude(b => b.BillingPeriod)
                        .Include(i => i.Apartment)
                        .Where(i => i.TenantUserId == userId)
                        .OrderByDescending(i => i.IssueDate)
                        .ToListAsync();
                }
            }
        }
    }
}

