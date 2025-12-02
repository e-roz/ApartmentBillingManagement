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
        public Lease? ActiveLease { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int PendingBills { get; set; }
        public decimal TotalPaid { get; set; }
        public List<BillSummary> RecentBills { get; set; } = new();

        // Alerts / flags for tenant dashboard
        public decimal OutstandingLateFees { get; set; }
        public bool HasOverdueRent { get; set; }
        public bool IsLeaseExpiringSoon { get; set; }

        // Upcoming dates for sidebar
        public DateTime? NextRentDueDate { get; set; }
        public DateTime? NextLateFeeDate { get; set; }

        // Recent transactions + maintenance status lists
        public List<TransactionSummary> RecentTransactions { get; set; } = new();
        public List<MaintenanceRequestSummary> MaintenanceRequests { get; set; } = new();

        // Calendar markers for rent due dates
        public HashSet<DateTime> RentDueDates { get; set; } = new();
        public HashSet<DateTime> OverdueRentDates { get; set; } = new();

        // Calendar selection (year / month)
        public int CalendarYear { get; set; }
        public int CalendarMonth { get; set; }

        public class BillSummary
        {
            public string BillingPeriod { get; set; } = string.Empty;
            public decimal AmountDue { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal RemainingBalance { get; set; }
            public DateTime DueDate { get; set; }
            public bool IsPaid { get; set; }
        }

        public class TransactionSummary
        {
            public DateTime Date { get; set; }
            public decimal Amount { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public class MaintenanceRequestSummary
        {
            public string Title { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime DateSubmitted { get; set; }
        }

        public async Task OnGetAsync(int? year, int? month)
        {
            Username = User.Identity?.Name ?? "Unknown User";

            // Determine which month the calendar should show
            var baseDate = DateTime.Today;
            if (year.HasValue && month.HasValue && month.Value is >= 1 and <= 12)
            {
                baseDate = new DateTime(year.Value, month.Value, 1);
            }
            CalendarYear = baseDate.Year;
            CalendarMonth = baseDate.Month;

            // Get user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // Count unread messages for the notification badge
                var unreadMessagesCount = await _context.Messages
                    .CountAsync(m => m.ReceiverUserId == userId && !m.IsRead);
                ViewData["UnreadMessagesCount"] = unreadMessagesCount;

                // Get user with active lease information
                var user = await _context.Users
                    .Include(u => u.Leases)
                        .ThenInclude(l => l.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    UserInfo = user;
                    var now = DateTime.UtcNow;
                    ActiveLease = user.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);
                    var unpaidBillSummaries = await _context.Bills
                        .Where(b => b.TenantUserId == user.Id)
                        .Select(b => new
                        {
                            AmountDue = b.AmountDue,
                            AmountPaid = b.PaymentAllocations.Sum(pa => pa.AmountApplied)
                        })
                        .Where(b => b.AmountDue > b.AmountPaid)
                        .ToListAsync();

                    OutstandingBalance = unpaidBillSummaries.Sum(b => b.AmountDue - b.AmountPaid);
                    PendingBills = unpaidBillSummaries.Count;

                    TotalPaid = await _context.PaymentAllocations
                        .Where(pa => pa.Bill.TenantUserId == userId)
                        .SumAsync(pa => pa.AmountApplied);

                    // Get all bills for additional calculations
                    var tenantBills = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Where(b => b.TenantUserId == user.Id)
                        .ToListAsync();

                    // Get the 10 most recent bills for display
                    var recentBillsFromDb = tenantBills
                        .OrderByDescending(b => b.DueDate)
                        .Take(10)
                        .Select(b => new BillSummary
                        {
                            BillingPeriod = b.BillingPeriod.MonthName + " " + b.BillingPeriod.Year,
                            AmountDue = b.AmountDue,
                            AmountPaid = b.PaymentAllocations.Sum(pa => pa.AmountApplied),
                            DueDate = b.DueDate
                        })
                        .ToList();

                    RecentBills = recentBillsFromDb.Select(b =>
                    {
                        b.RemainingBalance = Math.Max(0m, b.AmountDue - b.AmountPaid);
                        b.IsPaid = b.RemainingBalance <= 0m;
                        return b;
                    }).ToList();

                    // Alerts, calendar flags and upcoming dates
                    var nowLocal = DateTime.Now;
                    OutstandingLateFees = tenantBills
                        .Where(b => b.Type == Enums.BillType.LateFee && !b.IsPaid)
                        .Sum(b => Math.Max(0m, b.AmountDue - b.AmountPaid));

                    HasOverdueRent = tenantBills.Any(b =>
                        b.Type == Enums.BillType.Rent &&
                        !b.IsPaid &&
                        b.DueDate < nowLocal);

                    if (ActiveLease != null)
                    {
                        // Consider lease "expiring soon" within the next 30 days
                        IsLeaseExpiringSoon = ActiveLease.LeaseEnd.Date <= nowLocal.Date.AddDays(30);
                    }

                    // Calendar markers - rent due and overdue dates for selected month
                    var todayLocal = nowLocal.Date;
                    var startOfMonth = new DateTime(CalendarYear, CalendarMonth, 1);
                    var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                    var rentBillsThisMonth = tenantBills
                        .Where(b => b.Type == Enums.BillType.Rent
                                    && b.DueDate.Date >= startOfMonth
                                    && b.DueDate.Date <= endOfMonth)
                        .ToList();

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
                            // Paid rent still shows as a normal due date marker
                            RentDueDates.Add(billDate);
                        }
                    }

                    // Upcoming dates
                    NextRentDueDate = tenantBills
                        .Where(b => b.Type == Enums.BillType.Rent && !b.IsPaid && b.DueDate >= nowLocal.Date)
                        .OrderBy(b => b.DueDate)
                        .Select(b => (DateTime?)b.DueDate)
                        .FirstOrDefault();

                    NextLateFeeDate = tenantBills
                        .Where(b => b.Type == Enums.BillType.LateFee && !b.IsPaid && b.DueDate >= nowLocal.Date)
                        .OrderBy(b => b.DueDate)
                        .Select(b => (DateTime?)b.DueDate)
                        .FirstOrDefault();

                    // Recent transactions (based on invoices)
                    var recentInvoices = await _context.Invoices
                        .Where(i => i.TenantUserId == user.Id)
                        .OrderByDescending(i => i.DateFullySettled ?? i.IssueDate)
                        .Take(5)
                        .ToListAsync();

                    RecentTransactions = recentInvoices
                        .Select(i => new TransactionSummary
                        {
                            Date = i.DateFullySettled ?? i.IssueDate,
                            Amount = i.AmountDue,
                            Status = i.Status switch
                            {
                                Enums.InvoiceStatus.Paid => "Success",
                                Enums.InvoiceStatus.Cancelled => "Failed",
                                _ => i.Status.ToString()
                            }
                        })
                        .ToList();

                    // Maintenance requests submitted by the tenant
                    MaintenanceRequests = await _context.Requests
                        .Where(r => r.SubmittedByUserId == user.Id)
                        .OrderByDescending(r => r.DateSubmitted)
                        .Take(5)
                        .Select(r => new MaintenanceRequestSummary
                        {
                            Title = r.Title,
                            Status = r.Status.ToString(),
                            DateSubmitted = r.DateSubmitted
                        })
                        .ToListAsync();
                }
            }
        }
    }
}

