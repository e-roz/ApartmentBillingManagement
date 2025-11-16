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

                    var bills = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Where(b => b.TenantId == TenantInfo.Id && b.AmountPaid > 0)
                        .OrderByDescending(b => b.PaymentDate ?? b.GeneratedDate)
                        .ToListAsync();

                    Payments = bills.Select(b => new PaymentViewModel
                    {
                        TransactionId = b.Id,
                        PaymentDate = b.PaymentDate ?? b.GeneratedDate,
                        AmountPaid = b.AmountPaid,
                        PaymentMethod = "Online Payment",
                        InvoiceReference = $"Invoice #{b.Id}"
                    }).ToList();
                }
            }
        }
    }
}

