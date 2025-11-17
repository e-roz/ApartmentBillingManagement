using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages
{
    [Authorize(Roles = "Manager")]
    public class ManagerDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ManagerDashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Username { get; set; } = string.Empty;
        public int ActiveTenants { get; set; }
        public int OccupiedUnits { get; set; }
        public int PendingBills { get; set; }
        public decimal TotalRevenue { get; set; }

        public async Task OnGetAsync()
        {
            Username = User.Identity?.Name ?? "Unknown User";

            // Fetch manager-specific stats
            ActiveTenants = await _context.Tenants
                .Where(t => t.Status == LeaseStatus.Active)
                .CountAsync();

            OccupiedUnits = await _context.Apartments
                .Where(a => a.IsOccupied)
                .CountAsync();

            PendingBills = await _context.Bills
                .Where(b => b.AmountPaid < b.AmountDue)
                .CountAsync();

            TotalRevenue = await _context.Bills
                .Where(b => b.AmountPaid > 0)
                .SumAsync(b => b.AmountPaid);
        }
    }
}

