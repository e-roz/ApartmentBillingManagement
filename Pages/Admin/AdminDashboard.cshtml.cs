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
        
        // KPI Data
        public int TotalUsers { get; set; }
        public int TotalUnits { get; set; }
        public int OpenServiceTickets { get; set; }
        public int OverdueBills { get; set; }
        public List<AuditLog> RecentRegistrations { get; set; } = new List<AuditLog>();

        public async Task OnGetAsync()
        {
            // Get user information from claims
            Username = User.Identity?.Name ?? "Unknown User";
            UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown Role";

            // Fetch KPI data
            TotalUsers = await _context.Users.CountAsync();
            TotalUnits = await _context.Apartments.CountAsync();
            OpenServiceTickets = await _context.Requests.CountAsync(r => r.Status == Enums.RequestStatus.Submitted || r.Status == Enums.RequestStatus.InProgress);
            
            // Count overdue bills - calculate from actual invoice payments, not Bill.AmountPaid
            var today = DateTime.UtcNow.Date;
            var allBills = await _context.Bills
                .Where(b => b.DueDate < today)
                .Select(b => b.Id)
                .ToListAsync();

            var billIds = allBills.ToList();
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
                .GroupBy(i => i.BillId!.Value)
                .Select(group => new
                {
                    BillId = group.Key,
                    TotalPaid = group.Sum(i => i.AmountDue)
                })
                .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

            var billsWithAmounts = await _context.Bills
                .Where(b => b.DueDate < today)
                .Select(b => new { b.Id, b.AmountDue })
                .ToListAsync();

            OverdueBills = billsWithAmounts
                .Count(b => 
                {
                    var paidAmount = invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                    return b.AmountDue > paidAmount;
                });
            
            RecentRegistrations = await _context.AuditLogs
                .Where(a => a.Action == AuditActionType.CreateUser)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .Include(a => a.User)
                .ToListAsync();
        }
    }
}
