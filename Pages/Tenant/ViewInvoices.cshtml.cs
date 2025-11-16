using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class ViewInvoicesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewInvoicesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<InvoiceViewModel> Invoices { get; set; } = new();
        public Model.Tenant? TenantInfo { get; set; }

        public class InvoiceViewModel
        {
            public int InvoiceId { get; set; }
            public DateTime DateGenerated { get; set; }
            public DateTime DueDate { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal AmountPaid { get; set; }
            public string Status { get; set; } = string.Empty;
            public string BillingPeriod { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.Tenant != null)
                {
                    TenantInfo = user.Tenant;

                    var bills = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Where(b => b.TenantId == TenantInfo.Id)
                        .OrderByDescending(b => b.GeneratedDate)
                        .ToListAsync();

                    Invoices = bills.Select(b => new InvoiceViewModel
                    {
                        InvoiceId = b.Id,
                        DateGenerated = b.GeneratedDate,
                        DueDate = b.DueDate,
                        TotalAmount = b.AmountDue,
                        AmountPaid = b.AmountPaid,
                        BillingPeriod = b.BillingPeriod != null 
                            ? $"{b.BillingPeriod.MonthName} {b.BillingPeriod.Year}" 
                            : "N/A",
                        Status = b.IsPaid ? "Paid" 
                            : (b.DueDate < DateTime.Now ? "Overdue" : "Pending")
                    }).ToList();
                }
            }
        }
    }
}

