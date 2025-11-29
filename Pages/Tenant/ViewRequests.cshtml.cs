using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class ViewRequestsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewRequestsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Request> RequestList { get; set; } = new List<Request>();

        [TempData]
        public string SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                // This shouldn't happen if the user is authorized, but it's good practice to check.
                return Forbid();
            }

            RequestList = await _context.Requests
                .Where(r => r.SubmittedByUserId == userId)
                .OrderByDescending(r => r.DateSubmitted)
                .ToListAsync();

            return Page();
        }
    }
}
