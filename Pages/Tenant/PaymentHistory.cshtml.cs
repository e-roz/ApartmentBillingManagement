using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class PaymentHistoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public PaymentHistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<PaymentViewModel> Payments { get; set; } = new();
        public Model.Tenant? TenantInfo { get; set; }

        public class PaymentViewModel
        {
            public int TransactionId { get; set; }
            public DateTime PaymentDate { get; set; }
            public decimal AmountPaid { get; set; }
            public string PaymentMethod { get; set; } = "Online Payment";
            public string InvoiceReference { get; set; } = string.Empty;
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

                    var invoices = await _context.Invoices
                        .Where(i => i.TenantId == TenantInfo.Id && i.PaymentDate != null)
                        .OrderByDescending(i => i.PaymentDate)
                        .ToListAsync();

                    Payments = invoices.Select(i => new PaymentViewModel
                    {
                        TransactionId = i.Id,
                        PaymentDate = i.PaymentDate ?? i.IssueDate,
                        AmountPaid = i.AmountDue,
                        PaymentMethod = string.IsNullOrWhiteSpace(i.PaymentMethod)
                            ? "Online Payment"
                            : i.PaymentMethod,
                        InvoiceReference = !string.IsNullOrWhiteSpace(i.Title)
                            ? i.Title
                            : $"Bill #{i.BillId}"
                    }).ToList();
                }
            }
        }
    }
}

