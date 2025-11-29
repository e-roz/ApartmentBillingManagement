using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;

namespace Apartment.Pages.Manager
{
    [Authorize(Roles = "Admin")]
    public class ViewAllRequestsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewAllRequestsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Request> Requests { get; set; }

        public async Task OnGetAsync()
        {
            Requests = await _context.Requests
                .Include(r => r.SubmittedByUser)
                .Include(r => r.Apartment)
                .OrderByDescending(r => r.DateSubmitted)
                .ToListAsync();
        }
    }
}
