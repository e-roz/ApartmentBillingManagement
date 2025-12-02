using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class PaymentHistoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public PaymentHistoryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<PaymentViewModel> Payments { get; set; } = new();
        public Model.User? UserInfo { get; set; }

        public class PaymentViewModel
        {
            public int TransactionId { get; set; }
            public DateTime DateFullySettled { get; set; }
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
                    .Include(u => u.Leases)
                        .ThenInclude(l => l.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    UserInfo = user;

                    var invoices = await _context.Invoices
                        .Where(i => i.TenantUserId == userId && i.DateFullySettled != null)
                        .OrderByDescending(i => i.DateFullySettled)
                        .ToListAsync();

                    Payments = invoices.Select(i => new PaymentViewModel
                    {
                        TransactionId = i.Id,
                        DateFullySettled = i.DateFullySettled ?? i.IssueDate,
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

