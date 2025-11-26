using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AuditLogsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<AuditLog> AuditLogs { get; set; }

        public async Task OnGetAsync()
        {
            AuditLogs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(200) // Limit initial load for performance
                .ToListAsync();
        }
    }
}