using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class DocumentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DocumentsModel(ApplicationDbContext context)
        {
            _context = context;
        }

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
                }
            }
        }
    }
}

