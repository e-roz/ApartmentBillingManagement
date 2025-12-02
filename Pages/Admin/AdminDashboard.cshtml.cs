using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using Apartment.Enums;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Username { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        
        // Manager Dashboard KPI Data
        public int ActiveTenants { get; set; }
        public int OccupiedUnits { get; set; }
        public int OpenServiceTickets { get; set; }
        public int PendingBills { get; set; }
        public decimal TotalRevenue { get; set; }

        // Admin calendar + payment audit
        public HashSet<DateTime> RentDueDates { get; set; } = new();
        public HashSet<DateTime> OverdueRentDates { get; set; } = new();
        public List<PaymentAuditItem> RecentPayments { get; set; } = new();

        // Calendar selection (year / month)
        public int CalendarYear { get; set; }
        public int CalendarMonth { get; set; }

        public class PaymentAuditItem
        {
            public string TenantName { get; set; } = string.Empty;
            public string UnitNumber { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
        }

        public async Task OnGetAsync(int? year, int? month)
        {
            // Get user information from claims
            Username = User.Identity?.Name ?? "Unknown User";
            UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown Role";

            // Determine which month the calendar should show
            var baseDate = DateTime.Today;
            if (year.HasValue && month.HasValue && month.Value is >= 1 and <= 12)
            {
                baseDate = new DateTime(year.Value, month.Value, 1);
            }
            CalendarYear = baseDate.Year;
            CalendarMonth = baseDate.Month;

            // Fetch manager dashboard KPI data
            ActiveTenants = await _context.Users
                .Where(u => u.Role == UserRoles.Tenant && (u.Status == "Active" || u.Status == null))
                .CountAsync();

            OccupiedUnits = await _context.Apartments
                .Where(a => a.IsOccupied)
                .CountAsync();

            OpenServiceTickets = await _context.Requests.CountAsync(r => r.Status == Enums.RequestStatus.Submitted || r.Status == Enums.RequestStatus.InProgress);

            PendingBills = await _context.Bills
                .Where(b => b.AmountPaid < b.AmountDue)
                .CountAsync();

            TotalRevenue = await _context.Bills
                .Where(b => b.AmountPaid > 0)
                .SumAsync(b => b.AmountPaid);

            // Calendar data - rent due and overdue dates for selected month (portfolio-wide)
            var nowLocal = DateTime.Now;
            var todayLocal = nowLocal.Date;
            var startOfMonth = new DateTime(CalendarYear, CalendarMonth, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var rentBillsThisMonth = await _context.Bills
                .Where(b => b.Type == BillType.Rent
                            && b.DueDate.Date >= startOfMonth
                            && b.DueDate.Date <= endOfMonth)
                .ToListAsync();

            foreach (var bill in rentBillsThisMonth)
            {
                var billDate = bill.DueDate.Date;
                if (!bill.IsPaid)
                {
                    if (billDate < todayLocal)
                    {
                        OverdueRentDates.Add(billDate);
                    }
                    else
                    {
                        RentDueDates.Add(billDate);
                    }
                }
                else
                {
                    RentDueDates.Add(billDate);
                }
            }

            // Payment audit - last 3 paid invoices (FIFO: keep most recent 3)
            var recentPaidInvoices = await _context.Invoices
                .Include(i => i.TenantUser)
                .Where(i => i.Status == InvoiceStatus.Paid)
                .OrderByDescending(i => i.DateFullySettled ?? i.IssueDate)
                .Take(3)
                .ToListAsync();

            RecentPayments = recentPaidInvoices
                .Select(i => new PaymentAuditItem
                {
                    TenantName = i.TenantUser != null ? i.TenantUser.Username : "Unknown Tenant",
                    UnitNumber = i.Apartment?.UnitNumber ?? string.Empty,
                    Amount = i.AmountDue,
                    Date = i.DateFullySettled ?? i.IssueDate
                })
                .OrderBy(p => p.Date) // ensure FIFO order in display (oldest first)
                .ToList();
        }
    }
}
