using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class MakePaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MakePaymentModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PaymentInputModel Input { get; set; } = new();

        public decimal OutstandingBalance { get; set; }
        public Model.Tenant? TenantInfo { get; set; }
        public List<Bill> PendingBills { get; set; } = new();

        public class PaymentInputModel
        {
            public decimal Amount { get; set; }
            public int? BillId { get; set; }
            public string PaymentMethod { get; set; } = "Credit Card";
        }

        public async Task OnGetAsync(int? billId = null)
        {
            if (billId.HasValue)
            {
                Input.BillId = billId.Value;
            }
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDataAsync();
                return Page();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.Tenant != null)
                {
                    if (Input.BillId.HasValue)
                    {
                        var bill = await _context.Bills
                            .FirstOrDefaultAsync(b => b.Id == Input.BillId.Value && b.TenantId == user.Tenant.Id);

                        if (bill != null)
                        {
                            // Update Bill
                            bill.AmountPaid += Input.Amount;
                            bill.PaymentDate = DateTime.UtcNow;

                            // Find and update the corresponding Invoice
                            var invoice = await _context.Invoices
                                .FirstOrDefaultAsync(i => i.BillId == bill.Id);

                            if (invoice != null)
                            {
                                // Update Invoice PaymentDate
                                invoice.PaymentDate = DateTime.UtcNow;

                                // Update Invoice Status based on payment
                                if (bill.IsPaid)
                                {
                                    invoice.Status = InvoiceStatus.Paid;
                                }
                                else if (bill.DueDate < DateTime.UtcNow)
                                {
                                    invoice.Status = InvoiceStatus.Overdue;
                                }
                                else
                                {
                                    invoice.Status = InvoiceStatus.Pending;
                                }
                            }

                            await _context.SaveChangesAsync();

                            TempData["SuccessMessage"] = $"Payment of {Input.Amount:C} has been successfully recorded.";
                            return RedirectToPage("/Tenant/PaymentHistory");
                        }
                    }
                }
            }

            ModelState.AddModelError("", "Unable to process payment. Please try again.");
            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
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
                        .Where(b => b.TenantId == TenantInfo.Id && b.AmountPaid < b.AmountDue)
                        .OrderBy(b => b.DueDate)
                        .ToListAsync();

                    PendingBills = bills;
                    OutstandingBalance = bills.Sum(b => b.AmountDue - b.AmountPaid);
                }
            }
        }
    }
}

