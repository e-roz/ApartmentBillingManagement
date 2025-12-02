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
    [Authorize(Roles = "Tenant")]
    public class MakePaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private static readonly CultureInfo PhpCulture = CultureInfo.CreateSpecificCulture("en-PH");
        private const decimal AmountTolerance = 0.005m;

        public MakePaymentModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PaymentInputModel Input { get; set; } = new();

        public decimal OutstandingBalance { get; set; }
        public Model.User? UserInfo { get; set; }
        public List<Bill> PendingBills { get; set; } = new();

        public class PaymentInputModel
        {
            public decimal Amount { get; set; }
            public int? BillId { get; set; }
            public string PaymentMethod { get; set; } = string.Empty;
        }

        private static readonly HashSet<string> KnownPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "GCash", "Maya", "BDO", "Metrobank", "Landbank", "BPI"
        };

        public async Task<IActionResult> OnGetAsync(int? billId = null)
        {
            if (billId.HasValue)
            {
                Input.BillId = billId.Value;
            }
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            NormalizePaymentInput();

            var user = await GetUserFromClaimsAsync();
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find your user profile. Please contact support.");
                await LoadDataAsync();
                return Page();
            }

            // Perform initial validation checks
            if (Input.Amount <= 0)
                ModelState.AddModelError(nameof(Input.Amount), "Payment amount must be greater than zero.");
            if (!IsValidPaymentMethod(Input.PaymentMethod))
                ModelState.AddModelError(nameof(Input.PaymentMethod), "Please select a valid payment method.");
            
            if (!ModelState.IsValid)
            {
                await LoadDataAsync(user.Id);
                return Page();
            }

            var outstandingBills = await _context.Bills
                .Where(b => b.TenantUserId == user.Id && b.AmountDue > b.AmountPaid)
                .OrderBy(b => b.DueDate).ThenBy(b => b.Id)
                .ToListAsync();

            if (!outstandingBills.Any())
            {
                ModelState.AddModelError(string.Empty, "You have no outstanding bills to pay.");
                await LoadDataAsync(user.Id);
                return Page();
            }

            // Filter bills if a specific one is targeted
            var paymentPlan = outstandingBills;
            if (Input.BillId.HasValue)
            {
                paymentPlan = outstandingBills.Where(b => b.Id == Input.BillId.Value).ToList();
                if (!paymentPlan.Any())
                {
                    ModelState.AddModelError(nameof(Input.BillId), "The selected bill is already paid or does not exist.");
                    await LoadDataAsync(user.Id);
                    return Page();
                }
            }

            var totalOutstanding = paymentPlan.Sum(b => b.AmountDue - b.AmountPaid);
            if (totalOutstanding <= 0)
            {
                ModelState.AddModelError(nameof(Input.Amount), "No outstanding balance found for the selected bills.");
                await LoadDataAsync(user.Id);
                return Page();
            }

            decimal requiredAmount;
            string validationMessage;

            if (Input.BillId.HasValue)
            {
                var targetBill = paymentPlan.First();
                requiredAmount = targetBill.AmountDue - targetBill.AmountPaid;
                var formattedAmount = requiredAmount.ToString("C", PhpCulture);
                validationMessage = $"Please enter exactly {formattedAmount} for this month.";
            }
            else
            {
                requiredAmount = totalOutstanding;
                var formattedBalance = requiredAmount.ToString("C", PhpCulture);
                validationMessage = $"Please pay your total outstanding balance of {formattedBalance}.";
            }

            if (!MatchesExactAmount(Input.Amount, requiredAmount))
            {
                ModelState.AddModelError(nameof(Input.Amount), validationMessage);
                await LoadDataAsync(user.Id);
                return Page();
            }

            var (success, errorMessage) = await ExecutePaymentAsync(user.Id, paymentPlan, Input.Amount, Input.PaymentMethod);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, errorMessage ?? "An unexpected error occurred during payment.");
                await LoadDataAsync(user.Id);
                return Page();
            }

            TempData["SuccessMessage"] = $"Payment of {Input.Amount.ToString("C", PhpCulture)} was successfully recorded.";
            return RedirectToPage("/Tenant/PaymentHistory");
        }

        private async Task LoadDataAsync(int? userId = null)
        {
            var targetUserId = userId;
            if (!targetUserId.HasValue)
            {
                var user = await GetUserFromClaimsAsync();
                if (user == null) return;
                targetUserId = user.Id;
                UserInfo = user;
            }

            if (UserInfo == null && targetUserId.HasValue)
            {
                UserInfo = await _context.Users
                    .Include(u => u.Leases)
                        .ThenInclude(l => l.Apartment)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == targetUserId.Value);
            }

            if (targetUserId.HasValue)
            {
                PendingBills = await _context.Bills
                    .Include(b => b.BillingPeriod)
                    .Where(b => b.TenantUserId == targetUserId.Value && b.AmountDue > b.AmountPaid)
                    .OrderBy(b => b.DueDate).ThenBy(b => b.Id)
                    .AsNoTracking()
                    .ToListAsync();

                OutstandingBalance = PendingBills.Sum(b => b.AmountDue - b.AmountPaid);
            }
        }
        
        private async Task<Model.User?> GetUserFromClaimsAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Leases)
                        .ThenInclude(l => l.Apartment)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);
                return user;
            }
            return null;
        }

        private void NormalizePaymentInput()
        {
            Input.PaymentMethod = Input.PaymentMethod?.Trim() ?? string.Empty;
        }

        private bool IsValidPaymentMethod(string? method)
        {
            return !string.IsNullOrWhiteSpace(method) && KnownPaymentMethods.Contains(method.Trim());
        }

        private async Task<(bool Success, string? ErrorMessage)> ExecutePaymentAsync(
            int userId,
            List<Bill> paymentPlan,
            decimal paymentAmount,
            string paymentMethod)
        {
            if (!paymentPlan.Any() || paymentAmount <= 0)
            {
                return (false, "No bills to pay or payment amount is zero.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var amountLeftToAllocate = paymentAmount;
                var now = DateTime.UtcNow;

                // Create a single Invoice record for the entire payment
                var mainPaymentInvoice = new Invoice
                {
                    TenantUserId = userId,
                    ApartmentId = paymentPlan.FirstOrDefault()?.ApartmentId ?? 0, // Use first bill's ApartmentId or default
                    BillId = paymentPlan.Count == 1 ? paymentPlan.First().Id : (int?)null, // Conditionally set BillId
                    Title = $"Payment received - {paymentAmount.ToString("C", PhpCulture)}",
                    AmountDue = paymentAmount,
                    IssueDate = now,
                    DueDate = now.AddDays(7), // A reasonable due date for the payment invoice itself
                    DateFullySettled = now,
                    PaymentMethod = paymentMethod,
                    Status = InvoiceStatus.Paid,
                    ReferenceNumber = $"PAY-{now:yyyyMMddHHmmss}-{userId}" // Generate a unique reference
                };
                _context.Invoices.Add(mainPaymentInvoice);

                var allocations = new List<PaymentAllocation>();
                var billIds = paymentPlan.Select(b => b.Id).ToList();

                // Lock the specific bills we are about to work on to prevent concurrent modifications
                var billsToUpdate = await _context.Bills
                    .Include(b => b.BillingPeriod) // Ensure BillingPeriod is loaded
                    .Where(b => b.TenantUserId == userId && billIds.Contains(b.Id))
                    .OrderBy(b => b.DueDate).ThenBy(b => b.Id)
                    .ToListAsync();

                foreach (var bill in billsToUpdate)
                {
                    if (amountLeftToAllocate <= 0) break;

                    var remainingOnBill = bill.AmountDue - bill.AmountPaid;
                    if (remainingOnBill <= 0) continue;

                    var amountToApply = Math.Min(amountLeftToAllocate, remainingOnBill);

                    // 1. Update the Bill itself
                    bill.AmountPaid += amountToApply;
                    if (bill.AmountPaid >= bill.AmountDue)
                    {
                        bill.DateFullySettled ??= now;
                        bill.Status = BillStatus.Paid;
                    }
                    else
                    {
                        bill.Status = BillStatus.Unpaid;
                    }
                    
                    // 2. Create a corresponding PaymentAllocation record
                    var allocation = new PaymentAllocation
                    {
                        Invoice = mainPaymentInvoice, // Link to the main payment invoice
                        Bill = bill, // Link to the current bill
                        AmountApplied = amountToApply
                    };
                    allocations.Add(allocation);

                    amountLeftToAllocate -= amountToApply;
                }

                if (amountLeftToAllocate > 0.005m) // Allow for small rounding differences
                {
                    // This case should ideally not be hit if pre-validation is correct, but as a safeguard:
                    await transaction.RollbackAsync();
                    return (false, "Could not fully allocate payment. This may be due to a concurrent update. Please try again.");
                }

                _context.PaymentAllocations.AddRange(allocations);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                return (true, null);

            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                // Log the exception ex
                return (false, "A database error occurred while saving the payment. Please try again.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                // Log the exception ex
                return (false, "An unexpected error occurred. Please try again.");
            }
        }

        private static bool MatchesExactAmount(decimal amount, decimal expected) =>
            Math.Abs(amount - expected) <= AmountTolerance;
    }
}

