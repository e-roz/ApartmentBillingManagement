using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminDashBoardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AdminDashBoardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Username { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        
        // KPI Data
        public int TotalUsers { get; set; }
        public int TotalUnits { get; set; }
        public int OpenServiceTickets { get; set; } = 0; // Placeholder - no service ticket model yet
        public int OverdueBills { get; set; }

        public async Task OnGetAsync()
        {
            // Get user information from claims
            Username = User.Identity?.Name ?? "Unknown User";
            UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown Role";

            // Fetch KPI data
            TotalUsers = await _context.Users.CountAsync();
            TotalUnits = await _context.Apartments.CountAsync();
            
            // Count overdue bills (bills where AmountPaid < AmountDue and DueDate < today)
            OverdueBills = await _context.Bills
                .Where(b => b.AmountPaid < b.AmountDue && b.DueDate < DateTime.Now)
                .CountAsync();
        }
    }
}
