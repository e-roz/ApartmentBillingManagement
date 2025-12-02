using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Apartment.Enums;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class RecordPaymentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogSnagClient _logSnagClient;

        public RecordPaymentsModel(ApplicationDbContext context, IWebHostEnvironment environment, ILogSnagClient logSnagClient)
        {
            _context = context;
            _environment = environment;
            _logSnagClient = logSnagClient;
        }

        // Properties for display
        public List<TenantPaymentSummary> TenantSummaries { get; set; } = new();
        public List<MonthlyPaymentBreakdown> MonthlyBreakdowns { get; set; } = new();
        public PaymentSummaryViewModel? SelectedTenantSummary { get; set; }
        public List<SelectListItem> TenantOptions { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> MonthOptions { get; set; } = new();

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public int? SelectedTenantUserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterMonth { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        // Add Payment properties
        [BindProperty]
        public PaymentInputModel AddPaymentInput { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // View Models
        public class PaymentSummaryViewModel
        {
            public int TenantId { get; set; }
            public string TenantName { get; set; } = string.Empty;
            public string UnitNumber { get; set; } = string.Empty;
            public decimal TotalRent { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal RemainingBalance { get; set; }
            public DateTime? LastDateFullySettled { get; set; }
            public string? LatestPaymentMethod { get; set; }
            public string PaymentStatus { get; set; } = "Unpaid";
        }

        public class TenantPaymentSummary
        {
            public int TenantId { get; set; }
            public string TenantName { get; set; } = string.Empty;
            public string UnitNumber { get; set; } = string.Empty;
            public decimal MonthlyRent { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal RemainingBalance { get; set; }
            public string PaymentStatus { get; set; } = "Unpaid";
            public DateTime? LastDateFullySettled { get; set; }
        }

        public class MonthlyPaymentBreakdown
        {
            public int BillId { get; set; }
            public string Month { get; set; } = string.Empty;
            public int Year { get; set; }
            public DateTime DueDate { get; set; }
            public decimal RentAmount { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal RemainingBalance { get; set; }
            public string Status { get; set; } = "Unpaid";
            public DateTime? DateFullySettled { get; set; }
            public string? PaymentMethod { get; set; }
            public int? InvoiceId { get; set; }
            public string? ReferenceNumber { get; set; }
        }

        public class PaymentInputModel
        {
            public int TenantUserId { get; set; }
            public int BillId { get; set; }
            public decimal AmountPaid { get; set; }
            public DateTime DateFullySettled { get; set; } = DateTime.Now;
            public string PaymentMethod { get; set; } = "Cash";
            public string? Notes { get; set; }
            public string? MonthCovered { get; set; }
            public string? ReferenceNumber { get; set; }
        }

        public async Task OnGetAsync()
        {
            await PopulateFilterOptionsAsync();
            await LoadTenantSummariesAsync();
            
            if (SelectedTenantUserId.HasValue)
            {
                SelectedTenantSummary = await ComputePaymentSummaryAsync(SelectedTenantUserId.Value);
                await LoadMonthlyBreakdownAsync(SelectedTenantUserId.Value);
            }
        }

        public async Task<IActionResult> OnPostAddPaymentAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please fill in all required fields.";
                await OnGetAsync();
                return Page();
            }

            if (AddPaymentInput.AmountPaid <= 0)
            {
                ErrorMessage = "Payment amount must be greater than zero.";
                await OnGetAsync();
                return Page();
            }

            // Validate payment date is not in the future
            if (AddPaymentInput.DateFullySettled > DateTime.UtcNow)
            {
                ErrorMessage = "Payment date cannot be in the future.";
                await OnGetAsync();
                return Page();
            }

            Bill? targetBill = null; // Declare targetBill here so it's accessible in catch
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                Bill? bill;
                
                // Handle outstanding balance payment (BillId = 0 means auto-select oldest unpaid bill)
                if (AddPaymentInput.BillId == 0)
                {
                    // Find the oldest unpaid bill for this tenant user
                    var allBillsForTenant = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Include(b => b.TenantUser)
                        .Where(b => b.TenantUserId == AddPaymentInput.TenantUserId)
                        .OrderBy(b => b.DueDate)
                        .ToListAsync();

                    var billIds = allBillsForTenant.Select(b => b.Id).ToList();
                    
                    // Calculate actual paid amounts from invoices
                    var invoiceSums = await _context.Invoices
                        .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.DateFullySettled != null)
                        .GroupBy(i => i.BillId!.Value)
                        .Select(group => new
                        {
                            BillId = group.Key,
                            TotalPaid = group.Sum(i => i.AmountDue)
                        })
                        .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

                    // Find the oldest bill with remaining balance
                    bill = allBillsForTenant
                        .Where(b => 
                        {
                            var paidAmount = invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                            return b.AmountDue > paidAmount;
                        })
                        .OrderBy(b => b.DueDate)
                        .FirstOrDefault();

                    if (bill == null)
                    {
                        ErrorMessage = "No unpaid bills found for this tenant user.";
                        await OnGetAsync();
                        return Page();
                    }

                    // Auto-fill the remaining balance amount
                    var existingPaymentsForBill = invoiceSums.TryGetValue(bill.Id, out var paid) ? paid : 0m;
                    var remainingBalanceForBill = bill.AmountDue - existingPaymentsForBill;
                    
                    if (AddPaymentInput.AmountPaid == 0 || AddPaymentInput.AmountPaid > remainingBalanceForBill)
                    {
                        AddPaymentInput.AmountPaid = remainingBalanceForBill;
                    }
                    
                    // Update AddPaymentInput.BillId to the actual bill ID
                    AddPaymentInput.BillId = bill.Id;
                    
                    // Refetch the bill from context to ensure it's tracked and has latest state
                    bill = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Include(b => b.TenantUser)
                        .FirstOrDefaultAsync(b => b.Id == bill.Id && b.TenantUserId == AddPaymentInput.TenantUserId);
                    
                    if (bill == null)
                    {
                        ErrorMessage = "Bill not found after selection.";
                        await OnGetAsync();
                        return Page();
                    }
                }
                else
                {
                    bill = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Include(b => b.TenantUser)
                        .FirstOrDefaultAsync(b => b.Id == AddPaymentInput.BillId && b.TenantUserId == AddPaymentInput.TenantUserId);
                }

                if (bill == null)
                {
                    ErrorMessage = "Bill not found.";
                    await OnGetAsync();
                    return Page();
                }

                targetBill = bill;

                // Calculate actual remaining balance from invoices
                var existingPayments = await _context.Invoices
                    .Where(i => i.BillId == bill.Id && i.DateFullySettled != null)
                    .SumAsync(i => i.AmountDue);

                var remainingBalance = bill.AmountDue - existingPayments;

                if (remainingBalance <= 0)
                {
                    ErrorMessage = "This bill is already fully paid.";
                    await OnGetAsync();
                    return Page();
                }

                if (AddPaymentInput.AmountPaid > remainingBalance)
                {
                    ErrorMessage = $"Payment amount exceeds remaining balance of {remainingBalance:C}.";
                    await OnGetAsync();
                    return Page();
                }

                var newTotalPaid = existingPayments + AddPaymentInput.AmountPaid;
                var isFullyPaid = newTotalPaid >= bill.AmountDue;
                bill.AmountPaid = newTotalPaid;
                
                if (isFullyPaid)
                {
                    bill.DateFullySettled = AddPaymentInput.DateFullySettled;
                    bill.Status = BillStatus.Paid; 
                }
                else if (newTotalPaid > 0)
                {
                    bill.Status = BillStatus.Partial; 
                }

                // Create a single Invoice record for the entire payment
                var paymentInvoice = new Invoice
                {
                    TenantUserId = AddPaymentInput.TenantUserId,
                    ApartmentId = bill.ApartmentId, // Can be refined if multiple bills from different apartments are paid
                    BillId = null, // This invoice represents the overall payment
                    Title = bill.BillingPeriod != null
                        ? $"{bill.BillingPeriod.MonthName} {bill.BillingPeriod.Year} - Payment"
                        : $"Bill #{bill.Id} Payment", // Title might need adjustment for multi-bill payments
                    AmountDue = AddPaymentInput.AmountPaid, // Total amount paid in this transaction
                    DueDate = bill.DueDate, // Can be refined or set to a payment transaction date
                    IssueDate = DateTime.UtcNow,
                    DateFullySettled = AddPaymentInput.DateFullySettled,
                    PaymentMethod = AddPaymentInput.PaymentMethod,
                    Status = InvoiceStatus.Paid, // Status of the payment invoice itself
                    ReferenceNumber = AddPaymentInput.ReferenceNumber
                };
                _context.Invoices.Add(paymentInvoice);

                var allocations = new List<PaymentAllocation>();
                var amountLeftToAllocate = AddPaymentInput.AmountPaid;

                List<Bill> billsToApplyPaymentTo = new List<Bill>();

                if (AddPaymentInput.BillId == 0)
                {
                    // For "Pay Outstanding Balance", get all outstanding bills sorted by DueDate
                    billsToApplyPaymentTo = await _context.Bills
                        .Where(b => b.TenantUserId == AddPaymentInput.TenantUserId && (b.Status == BillStatus.Unpaid || b.Status == BillStatus.Partial))
                        .OrderBy(b => b.DueDate)
                        .ToListAsync();
                }
                else
                {
                    // For specific bill, just get that one
                    billsToApplyPaymentTo.Add(bill);
                }

                foreach (var billToUpdate in billsToApplyPaymentTo)
                {
                    if (amountLeftToAllocate <= 0) break;

                    var remainingOnBill = billToUpdate.AmountDue - billToUpdate.AmountPaid;
                    if (remainingOnBill <= 0) continue; // Skip if already fully paid

                    var amountToApply = Math.Min(amountLeftToAllocate, remainingOnBill);

                    // Update the Bill itself
                    billToUpdate.AmountPaid += amountToApply;
                    if (billToUpdate.AmountPaid >= billToUpdate.AmountDue)
                    {
                        billToUpdate.DateFullySettled ??= AddPaymentInput.DateFullySettled;
                        billToUpdate.Status = BillStatus.Paid;
                    }
                    else if (billToUpdate.AmountPaid > 0)
                    {
                        billToUpdate.Status = BillStatus.Partial;
                    }
                    // If still unpaid, status remains Unpaid (default)

                    // Create a corresponding PaymentAllocation record
                    var allocation = new PaymentAllocation
                    {
                        Invoice = paymentInvoice, // Link to the main payment invoice
                        Bill = billToUpdate, // Link to the current bill
                        AmountApplied = amountToApply
                    };
                    allocations.Add(allocation);

                    amountLeftToAllocate -= amountToApply;
                }

                if (amountLeftToAllocate > 0.005m) // Allow for small rounding differences
                {
                    ErrorMessage = "Could not fully allocate payment. Remaining amount: " + amountLeftToAllocate.ToString("C", CultureInfo.CurrentCulture);
                    await OnGetAsync(); // Re-load data for the page
                    await transaction.RollbackAsync();
                    return Page();
                }

                _context.PaymentAllocations.AddRange(allocations);
                _context.Bills.UpdateRange(billsToApplyPaymentTo); // Ensure bills are marked for update

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _logSnagClient.PublishAsync(new LogSnagEvent
                {
                    Event = "Payment Recorded",
                    Description = $"{targetBill?.TenantUser?.Username ?? "Unknown"} paid {AddPaymentInput.AmountPaid:C} for Bill #{targetBill?.Id}",
                    Icon = "💰",
                    Tags = new Dictionary<string, string>
                    {
                        { "tenant", targetBill?.TenantUser?.Username ?? "Unknown" },
                        { "billId", targetBill?.Id.ToString() ?? "N/A" },
                        { "amount", AddPaymentInput.AmountPaid.ToString("0.##") }
                    }
                });

                SuccessMessage = $"Payment of {AddPaymentInput.AmountPaid:C} has been successfully recorded.";
                SelectedTenantUserId = AddPaymentInput.TenantUserId;
                return RedirectToPage("/Admin/RecordPayments", new { SelectedTenantUserId = AddPaymentInput.TenantUserId });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred while processing the payment: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
        }

        // Helper Methods
        private async Task<PaymentSummaryViewModel> ComputePaymentSummaryAsync(int tenantUserId)
        {
            var tenantUser = await _context.Users
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .FirstOrDefaultAsync(u => u.Id == tenantUserId && u.Role == UserRoles.Tenant);

            if (tenantUser == null)
            {
                return new PaymentSummaryViewModel { TenantId = tenantUserId };
            }

            // Get all bills for this tenant user
            var bills = await _context.Bills
                .Include(b => b.BillingPeriod)
                .Where(b => b.TenantUserId == tenantUserId)
                .ToListAsync();

            var billIds = bills.Select(b => b.Id).ToList();

            // Calculate actual paid amounts from invoices
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.DateFullySettled != null)
                .GroupBy(i => i.BillId!.Value)
                .Select(group => new
                {
                    BillId = group.Key,
                    TotalPaid = group.Sum(i => i.AmountDue)
                })
                .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

            // Note: bill.AmountPaid is only updated for display purposes here (bills are AsNoTracking)
            // The actual AmountPaid should always be calculated from invoices when needed
            foreach (var bill in bills)
            {
                if (invoiceSums.TryGetValue(bill.Id, out var paidAmount))
                {
                    bill.AmountPaid = paidAmount; // Only for display, not persisted
                }
            }

            // Get all payment invoices
            var paymentInvoices = await _context.Invoices
                .Where(i => i.TenantUserId == tenantUserId && i.DateFullySettled != null)
                .OrderByDescending(i => i.DateFullySettled)
                .ToListAsync();

            var totalRent = bills.Sum(b => b.AmountDue);
            var totalPaid = bills.Sum(b => b.AmountPaid);
            var remainingBalance = totalRent - totalPaid;

            var lastPayment = paymentInvoices.FirstOrDefault();
            var latestPaymentMethod = lastPayment?.PaymentMethod ?? "N/A";

            var today = DateTime.UtcNow.Date;
            bool hasOverdueBills = bills.Any(b => (b.Status == BillStatus.Unpaid || b.Status == BillStatus.Partial) && b.DueDate < today);

            string paymentStatus;
            if (hasOverdueBills)
            {
                paymentStatus = "Overdue";
            }
            else if (bills.All(b => b.Status == BillStatus.Paid))
            {
                paymentStatus = "Paid";
            }
            else if (bills.Any(b => b.Status == BillStatus.Partial))
            {
                paymentStatus = "Partial";
            }
            else
            {
                paymentStatus = "Unpaid";
            }

            var now = DateTime.UtcNow;
            var activeLease = tenantUser.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);
            return new PaymentSummaryViewModel
            {
                TenantId = tenantUserId,
                TenantName = tenantUser.Username,
                UnitNumber = activeLease?.UnitNumber ?? "Unassigned",
                TotalRent = totalRent,
                AmountPaid = totalPaid,
                RemainingBalance = remainingBalance,
                LastDateFullySettled = lastPayment?.DateFullySettled,
                LatestPaymentMethod = latestPaymentMethod,
                PaymentStatus = paymentStatus
            };
        }

        private async Task LoadTenantSummariesAsync()
        {
            var now = DateTime.UtcNow;
            var query = _context.Users
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .Where(u => u.Role == UserRoles.Tenant)
                .AsQueryable();

            // Apply search filter with sanitization
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                // Sanitize and limit search term length to prevent abuse
                var sanitizedTerm = SearchTerm.Trim();
                if (sanitizedTerm.Length > 100)
                {
                    sanitizedTerm = sanitizedTerm.Substring(0, 100);
                }
                
                query = query.Where(u => 
                    u.Username.Contains(sanitizedTerm) || 
                    (u.Leases != null && u.Leases.Any(l => l.LeaseEnd >= now && l.Apartment.UnitNumber.Contains(sanitizedTerm))) ||
                    u.Email.Contains(sanitizedTerm));
            }

            var tenantUsers = await query.ToListAsync();
            var tenantUserIds = tenantUsers.Select(u => u.Id).ToList();

            // Get all bills for these tenant users
            var allBills = await _context.Bills
                .Where(b => tenantUserIds.Contains(b.TenantUserId))
                .ToListAsync();

            var billIds = allBills.Select(b => b.Id).ToList();

            // Calculate actual paid amounts from invoices
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.DateFullySettled != null)
                .GroupBy(i => i.BillId!.Value)
                .Select(group => new
                {
                    BillId = group.Key,
                    TotalPaid = group.Sum(i => i.AmountDue)
                })
                .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

            // Note: bill.AmountPaid is only updated for display purposes here
            // The actual AmountPaid should always be calculated from invoices when needed
            foreach (var bill in allBills)
            {
                if (invoiceSums.TryGetValue(bill.Id, out var totalPaid))
                {
                    bill.AmountPaid = totalPaid; // Only for display, not persisted
                }
            }

            // Get last payments for each tenant user
            var lastPayments = await _context.Invoices
                .Where(i => tenantUserIds.Contains(i.TenantUserId) && i.DateFullySettled != null)
                .GroupBy(i => i.TenantUserId)
                .Select(g => new
                {
                    TenantUserId = g.Key,
                    LastPayment = g.OrderByDescending(i => i.DateFullySettled).FirstOrDefault()
                })
                .ToDictionaryAsync(k => k.TenantUserId, v => v.LastPayment);

            TenantSummaries = tenantUsers.Select(u =>
            {
                var bills = allBills.Where(b => b.TenantUserId == u.Id).ToList();
                
                // Calculate totals for ALL bills (not just unpaid) for accurate TotalPaid display
                var totalRentAllBills = bills.Sum(b => b.AmountDue);
                var totalPaidAllBills = bills.Sum(b => 
                {
                    return invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                });
                
                // Get only unpaid bills (bills with remaining balance) for remaining balance calculation
                var unpaidBills = bills.Where(b =>
                {
                    var paidAmount = invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                    return b.AmountDue > paidAmount;
                }).ToList();
                
                var totalRentUnpaid = unpaidBills.Sum(b => b.AmountDue);
                var totalPaidUnpaid = unpaidBills.Sum(b => 
                {
                    return invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                });
                var remainingBalance = totalRentUnpaid - totalPaidUnpaid;

                lastPayments.TryGetValue(u.Id, out var lastPayment);

                var today = DateTime.UtcNow.Date;
                bool hasOverdueBills = bills.Any(b => (b.Status == BillStatus.Unpaid || b.Status == BillStatus.Partial) && b.DueDate < today);

                string paymentStatus;
                if (hasOverdueBills)
                {
                    paymentStatus = "Overdue";
                }
                else if (bills.All(b => b.Status == BillStatus.Paid))
                {
                    paymentStatus = "Paid";
                }
                else if (bills.Any(b => b.Status == BillStatus.Partial))
                {
                    paymentStatus = "Partial";
                }
                else
                {
                    paymentStatus = "Unpaid";
                }

                var activeLease = u.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);
                return new TenantPaymentSummary
                {
                    TenantId = u.Id,
                    TenantName = u.Username,
                    UnitNumber = activeLease?.UnitNumber ?? "Unassigned",
                    MonthlyRent = activeLease?.MonthlyRent ?? 0m,
                    TotalPaid = totalPaidAllBills, // Total paid across ALL bills for accurate display
                    RemainingBalance = remainingBalance,
                    PaymentStatus = paymentStatus,
                    LastDateFullySettled = lastPayment?.DateFullySettled
                };
            }).ToList();

            // Apply status filter
            if (!string.IsNullOrEmpty(FilterStatus))
            {
                TenantSummaries = TenantSummaries
                    .Where(t => t.PaymentStatus.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private async Task LoadMonthlyBreakdownAsync(int tenantUserId)
        {
            var bills = await _context.Bills
                .Include(b => b.BillingPeriod)
                .Where(b => b.TenantUserId == tenantUserId)
                .OrderByDescending(b => b.BillingPeriod.Year)
                .ThenByDescending(b => b.BillingPeriod.MonthName)
                .ToListAsync();

            var billIds = bills.Select(b => b.Id).ToList();

            // Calculate actual paid amounts from invoices
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.DateFullySettled != null)
                .GroupBy(i => i.BillId!.Value)
                .Select(group => new
                {
                    BillId = group.Key,
                    TotalPaid = group.Sum(i => i.AmountDue)
                })
                .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

            var breakdowns = new List<MonthlyPaymentBreakdown>();

            foreach (var bill in bills)
            {
                // Get actual paid amount from invoices
                var actualPaid = invoiceSums.TryGetValue(bill.Id, out var totalPaid) ? totalPaid : 0m;
                
                // Note: bill.AmountPaid is only updated for display purposes here (bill is AsNoTracking)
                // The actual AmountPaid should always be calculated from invoices when needed
                bill.AmountPaid = actualPaid; // Only for display, not persisted

                var latestPayment = await _context.Invoices
                    .Where(i => i.BillId == bill.Id && i.DateFullySettled != null)
                    .OrderByDescending(i => i.DateFullySettled)
                    .FirstOrDefaultAsync();

                var monthName = bill.BillingPeriod?.MonthName ?? "Unknown";
                var year = bill.BillingPeriod?.Year ?? DateTime.Now.Year;
                var status = bill.Status.ToString();

                breakdowns.Add(new MonthlyPaymentBreakdown
                {
                    BillId = bill.Id,
                    Month = monthName,
                    Year = year,
                    DueDate = bill.DueDate,
                    RentAmount = bill.AmountDue,
                    PaidAmount = actualPaid,
                    RemainingBalance = bill.AmountDue - actualPaid,
                    Status = status,
                    DateFullySettled = latestPayment?.DateFullySettled,
                    PaymentMethod = latestPayment?.PaymentMethod,
                    InvoiceId = latestPayment?.Id,
                    ReferenceNumber = latestPayment?.ReferenceNumber
                });
            }

            // Apply month filter
            if (!string.IsNullOrEmpty(FilterMonth))
            {
                breakdowns = breakdowns
                    .Where(b => b.Month.Equals(FilterMonth, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(FilterStatus))
            {
                breakdowns = breakdowns
                    .Where(b => b.Status.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            MonthlyBreakdowns = breakdowns;
        }

        private async Task PopulateFilterOptionsAsync()
        {
            // Tenant user options
            var now = DateTime.UtcNow;
            var tenantUsers = await _context.Users
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .Where(u => u.Role == UserRoles.Tenant)
                .ToListAsync();

            var tenantUserOptions = tenantUsers.Select(u =>
            {
                var activeLease = u.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);
                return new { u.Id, u.Username, UnitNumber = activeLease?.UnitNumber ?? "Unassigned" };
            }).OrderBy(u => u.Username).ToList();

            TenantOptions = tenantUserOptions.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = $"{u.Username} - {u.UnitNumber}",
                Selected = SelectedTenantUserId == u.Id
            }).ToList();

            TenantOptions.Insert(0, new SelectListItem("All Tenants", "", !SelectedTenantUserId.HasValue));

            // Status options
            StatusOptions = new List<SelectListItem>
            {
                new SelectListItem("All Statuses", "", string.IsNullOrEmpty(FilterStatus)),
                new SelectListItem("Paid", "Paid", FilterStatus == "Paid"),
                new SelectListItem("Partial", "Partial", FilterStatus == "Partial"),
                new SelectListItem("Unpaid", "Unpaid", FilterStatus == "Unpaid"),
                new SelectListItem("Overdue", "Overdue", FilterStatus == "Overdue"),
                new SelectListItem("Advance", "Advance", FilterStatus == "Advance")
            };

            // Month options
            var months = new[] { "January", "February", "March", "April", "May", "June",
                "July", "August", "September", "October", "November", "December" };

            MonthOptions = months.Select(m => new SelectListItem
            {
                Value = m,
                Text = m,
                Selected = FilterMonth == m
            }).ToList();

            MonthOptions.Insert(0, new SelectListItem("All Months", "", string.IsNullOrEmpty(FilterMonth)));
        }
    }
}
