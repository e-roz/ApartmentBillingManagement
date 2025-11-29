using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages
{
    [Authorize(Roles = "Tenant")]
    public class TenantDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TenantDashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Username { get; set; } = string.Empty;
        public Model.User? UserInfo { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int PendingBills { get; set; }
        public decimal TotalPaid { get; set; }
        public List<BillSummary> RecentBills { get; set; } = new();

        public class BillSummary
        {
            public string BillingPeriod { get; set; } = string.Empty;
            public decimal AmountDue { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal RemainingBalance { get; set; }
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
                // Count unread messages for the notification badge
                var unreadMessagesCount = await _context.Messages
                    .CountAsync(m => m.ReceiverUserId == userId && !m.IsRead);
                ViewData["UnreadMessagesCount"] = unreadMessagesCount;

                // Get user with apartment information
                var user = await _context.Users
                    .Include(u => u.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    UserInfo = user;
                    // Get bills for this user
                    var bills = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Where(b => b.TenantUserId == user.Id)
                        .OrderByDescending(b => b.DueDate)
                        .Take(10)
                        .ToListAsync();

                    var billIds = bills.Select(b => b.Id).ToList();
                    var invoiceSums = await _context.Invoices
                        .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
                        .GroupBy(i => i.BillId!.Value)
                        .Select(group => new
                        {
                            BillId = group.Key,
                            TotalPaid = group.Sum(i => i.AmountDue)
                        })
                        .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

                    decimal GetPaidAmount(int billId) =>
                        invoiceSums.TryGetValue(billId, out var totalPaid) ? totalPaid : 0m;

                    var billSummaries = bills.Select(b =>
                    {
                        var paid = GetPaidAmount(b.Id);
                        var remaining = Math.Max(0m, b.AmountDue - paid);
                        return new BillSummary
                        {
                            BillingPeriod = b.BillingPeriod?.MonthName + " " + b.BillingPeriod?.Year ?? "N/A",
                            AmountDue = b.AmountDue,
                            AmountPaid = paid,
                            RemainingBalance = remaining,
                            DueDate = b.DueDate,
                            IsPaid = remaining == 0m
                        };
                    }).ToList();

                    OutstandingBalance = billSummaries
                        .Where(b => b.RemainingBalance > 0)
                        .Sum(b => b.RemainingBalance);

                    PendingBills = billSummaries.Count(b => b.RemainingBalance > 0);

                    TotalPaid = billSummaries.Sum(b => b.AmountPaid);

                    RecentBills = billSummaries;
                }
            }
        }
    }
}

