using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages
{
    [Authorize(Roles = "User")]
    public class TenantDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TenantDashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Username { get; set; } = string.Empty;
        public Tenant? TenantInfo { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int PendingBills { get; set; }
        public decimal TotalPaid { get; set; }
        public List<BillSummary> RecentBills { get; set; } = new();

        public class BillSummary
        {
            public string BillingPeriod { get; set; } = string.Empty;
            public decimal AmountDue { get; set; }
            public decimal AmountPaid { get; set; }
            public DateTime DueDate { get; set; }
            public bool IsPaid { get; set; }
        }

        public async Task OnGetAsync()
        {
            Username = User.Identity?.Name ?? "Unknown User";

            // Get user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Find tenant linked to this user via User.TenantID
                var user = await _context.Users
                    .Include(u => u.Tenant)
                    .ThenInclude(t => t!.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.Tenant != null)
                {
                    TenantInfo = user.Tenant;
                    // Get bills for this tenant
                    var bills = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Where(b => b.TenantId == TenantInfo.Id)
                        .OrderByDescending(b => b.DueDate)
                        .Take(10)
                        .ToListAsync();

                    OutstandingBalance = bills
                        .Where(b => b.AmountPaid < b.AmountDue)
                        .Sum(b => b.AmountDue - b.AmountPaid);

                    PendingBills = bills.Count(b => b.AmountPaid < b.AmountDue);

                    TotalPaid = bills.Sum(b => b.AmountPaid);

                    RecentBills = bills.Select(b => new BillSummary
                    {
                        BillingPeriod = b.BillingPeriod?.MonthName + " " + b.BillingPeriod?.Year ?? "N/A",
                        AmountDue = b.AmountDue,
                        AmountPaid = b.AmountPaid,
                        DueDate = b.DueDate,
                        IsPaid = b.IsPaid
                    }).ToList();
                }
            }
        }
    }
}

