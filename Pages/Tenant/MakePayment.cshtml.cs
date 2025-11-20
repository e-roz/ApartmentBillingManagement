using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class MakePaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
            private static readonly CultureInfo PhpCulture = CultureInfo.CreateSpecificCulture("en-PH");

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
            if (string.IsNullOrWhiteSpace(Input.PaymentMethod))
            {
                ModelState.AddModelError(nameof(Input.PaymentMethod), "Please select a payment method.");
            }

            if (Input.Amount <= 0)
            {
                ModelState.AddModelError(nameof(Input.Amount), "Payment amount must be greater than zero.");
            }

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
                            .Include(b => b.BillingPeriod)
                            .FirstOrDefaultAsync(b => b.Id == Input.BillId.Value && b.TenantId == user.Tenant.Id);

                        if (bill != null)
                        {
                            var tenantMonthlyRent = user.Tenant.MonthlyRent;

                            if (tenantMonthlyRent > 0 && Input.Amount > tenantMonthlyRent)
                            {
                                ModelState.AddModelError(nameof(Input.Amount), $"Payment amount cannot exceed your monthly rent of {tenantMonthlyRent.ToString("C", PhpCulture)}.");
                                await LoadDataAsync();
                                return Page();
                            }

                            var existingPayments = await _context.Invoices
                                .Where(i => i.BillId == bill.Id && i.PaymentDate != null)
                                .SumAsync(i => i.AmountDue);

                            bill.AmountPaid = existingPayments;

                            var remainingBalance = bill.AmountDue - bill.AmountPaid;

                            if (remainingBalance <= 0)
                            {
                                ModelState.AddModelError(nameof(Input.BillId), "This bill has already been paid in full.");
                                await LoadDataAsync();
                                return Page();
                            }

                            if (Input.Amount > remainingBalance)
                            {
                                ModelState.AddModelError(nameof(Input.Amount), "Payment amount exceeds the remaining balance for this bill.");
                                await LoadDataAsync();
                                return Page();
                            }

                            bill.AmountPaid += Input.Amount;

                            var now = DateTime.UtcNow;
                            if (bill.AmountPaid == bill.AmountDue)
                            {
                                bill.PaymentDate = now;
                            }

                            var invoiceStatus = bill.AmountPaid == bill.AmountDue
                                ? InvoiceStatus.Paid
                                : InvoiceStatus.Partial;

                            var paymentInvoice = new Invoice
                            {
                                BillId = bill.Id,
                                TenantId = bill.TenantId,
                                ApartmentId = bill.ApartmentId,
                                Title = bill.BillingPeriod != null
                                    ? $"{bill.BillingPeriod.MonthName} {bill.BillingPeriod.Year} - Payment"
                                    : $"Bill #{bill.Id} Payment",
                                AmountDue = Input.Amount,
                                DueDate = bill.DueDate,
                                IssueDate = now,
                                PaymentDate = now,
                                PaymentMethod = Input.PaymentMethod,
                                Status = invoiceStatus
                            };

                            _context.Invoices.Add(paymentInvoice);

                            await _context.SaveChangesAsync();

                            TempData["SuccessMessage"] = $"Payment of {Input.Amount.ToString("C", PhpCulture)} has been successfully recorded.";
                            return RedirectToPage("/Tenant/PaymentHistory");
                        }
                    }
                    else
                    {
                        var unpaidBills = await _context.Bills
                            .Include(b => b.BillingPeriod)
                            .Where(b => b.TenantId == user.Tenant.Id)
                            .OrderBy(b => b.DueDate)
                            .ToListAsync();

                        var billIds = unpaidBills.Select(b => b.Id).ToList();

                        var invoiceSums = await _context.Invoices
                            .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
                            .GroupBy(i => i.BillId!.Value)
                            .Select(group => new
                            {
                                BillId = group.Key,
                                TotalPaid = group.Sum(i => i.AmountDue)
                            })
                            .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

                        var outstandingBills = unpaidBills
                            .Select(b => new
                            {
                                Bill = b,
                                Paid = invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m
                            })
                            .Where(x => x.Bill.AmountDue > x.Paid)
                            .OrderBy(x => x.Bill.DueDate)
                            .ToList();

                        if (!outstandingBills.Any())
                        {
                            ModelState.AddModelError(nameof(Input.BillId), "You have no outstanding bills to pay.");
                            await LoadDataAsync();
                            return Page();
                        }

                        var outstandingBalance = outstandingBills.Sum(x => x.Bill.AmountDue - x.Paid);

                        if (Input.Amount > outstandingBalance)
                        {
                            ModelState.AddModelError(nameof(Input.Amount), $"Payment amount exceeds your outstanding balance of {outstandingBalance.ToString("C", PhpCulture)}.");
                            await LoadDataAsync();
                            return Page();
                        }

                        var amountToAllocate = Input.Amount;
                        var now = DateTime.UtcNow;

                        foreach (var entry in outstandingBills)
                        {
                            if (amountToAllocate <= 0)
                            {
                                break;
                            }

                            var remainingForBill = entry.Bill.AmountDue - entry.Paid;
                            if (remainingForBill <= 0)
                            {
                                continue;
                            }

                            var amountApplied = Math.Min(remainingForBill, amountToAllocate);
                            var newTotalPaid = entry.Paid + amountApplied;
                            var billFullyPaid = newTotalPaid >= entry.Bill.AmountDue;

                            entry.Bill.AmountPaid = newTotalPaid;
                            if (billFullyPaid)
                            {
                                entry.Bill.PaymentDate = now;
                            }

                            var invoiceStatus = billFullyPaid ? InvoiceStatus.Paid : InvoiceStatus.Partial;

                            var invoice = new Invoice
                            {
                                BillId = entry.Bill.Id,
                                TenantId = entry.Bill.TenantId,
                                ApartmentId = entry.Bill.ApartmentId,
                                Title = entry.Bill.BillingPeriod != null
                                    ? $"{entry.Bill.BillingPeriod.MonthName} {entry.Bill.BillingPeriod.Year} - Payment"
                                    : $"Bill #{entry.Bill.Id} Payment",
                                AmountDue = amountApplied,
                                DueDate = entry.Bill.DueDate,
                                IssueDate = now,
                                PaymentDate = now,
                                PaymentMethod = Input.PaymentMethod,
                                Status = invoiceStatus
                            };

                            _context.Invoices.Add(invoice);

                            amountToAllocate -= amountApplied;
                        }

                        if (amountToAllocate > 0)
                        {
                            ModelState.AddModelError(nameof(Input.Amount), "Unable to allocate the full payment amount. Please try again.");
                            await LoadDataAsync();
                            return Page();
                        }

                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = $"Payment of {Input.Amount.ToString("C", PhpCulture)} has been successfully recorded.";
                        return RedirectToPage("/Tenant/PaymentHistory");
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
                        .AsNoTracking()
                        .OrderBy(b => b.DueDate)
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

                    foreach (var bill in bills)
                    {
                        if (invoiceSums.TryGetValue(bill.Id, out var totalPaid))
                        {
                            bill.AmountPaid = totalPaid;
                        }
                    }

                    PendingBills = bills;
                    OutstandingBalance = bills.Sum(b => Math.Max(0m, b.AmountDue - b.AmountPaid));
                }
            }
        }
    }
}

