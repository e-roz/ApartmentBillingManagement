using Apartment.Data;
using Apartment.Model;
using Apartment.Enums;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;

namespace Apartment.Pages.Manager
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
        public int? SelectedTenantId { get; set; }

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
            public DateTime? LastPaymentDate { get; set; }
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
            public DateTime? LastPaymentDate { get; set; }
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
            public DateTime? PaymentDate { get; set; }
            public string? PaymentMethod { get; set; }
            public int? InvoiceId { get; set; }
            public string? ReceiptImagePath { get; set; }
            public string? ReferenceNumber { get; set; }
        }

        public class PaymentInputModel
        {
            public int TenantId { get; set; }
            public int BillId { get; set; }
            public decimal AmountPaid { get; set; }
            public DateTime PaymentDate { get; set; } = DateTime.Now;
            public string PaymentMethod { get; set; } = "Cash";
            public string? Notes { get; set; }
            public string? MonthCovered { get; set; }
            public string? ReferenceNumber { get; set; }
        }

        public async Task OnGetAsync()
        {
            await PopulateFilterOptionsAsync();
            await LoadTenantSummariesAsync();
            
            if (SelectedTenantId.HasValue)
            {
                SelectedTenantSummary = await ComputePaymentSummaryAsync(SelectedTenantId.Value);
                await LoadMonthlyBreakdownAsync(SelectedTenantId.Value);
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
            if (AddPaymentInput.PaymentDate > DateTime.UtcNow)
            {
                ErrorMessage = "Payment date cannot be in the future.";
                await OnGetAsync();
                return Page();
            }

            try
            {
                Bill? bill;
                
                // Handle outstanding balance payment (BillId = 0 means auto-select oldest unpaid bill)
                if (AddPaymentInput.BillId == 0)
                {
                    // Find the oldest unpaid bill for this tenant
                    var allBillsForTenant = await _context.Bills
                        .Include(b => b.BillingPeriod)
                        .Include(b => b.Tenant)
                        .Where(b => b.TenantId == AddPaymentInput.TenantId)
                        .OrderBy(b => b.DueDate)
                        .ToListAsync();

                    var billIds = allBillsForTenant.Select(b => b.Id).ToList();
                    
                    // Calculate actual paid amounts from invoices
                    var invoiceSums = await _context.Invoices
                        .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
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
                        ErrorMessage = "No unpaid bills found for this tenant.";
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
                        .Include(b => b.Tenant)
                        .FirstOrDefaultAsync(b => b.Id == bill.Id && b.TenantId == AddPaymentInput.TenantId);
                    
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
                        .Include(b => b.Tenant)
                        .FirstOrDefaultAsync(b => b.Id == AddPaymentInput.BillId && b.TenantId == AddPaymentInput.TenantId);
                }

                if (bill == null)
                {
                    ErrorMessage = "Bill not found.";
                    await OnGetAsync();
                    return Page();
                }

                // Calculate actual remaining balance from invoices
                var existingPayments = await _context.Invoices
                    .Where(i => i.BillId == bill.Id && i.PaymentDate != null)
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
                    bill.PaymentDate = AddPaymentInput.PaymentDate;
                }

                // Create invoice for payment - use newTotalPaid to determine status
                var invoiceStatus = isFullyPaid
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
                    AmountDue = AddPaymentInput.AmountPaid,
                    DueDate = bill.DueDate,
                    IssueDate = DateTime.UtcNow,
                    PaymentDate = AddPaymentInput.PaymentDate,
                    PaymentMethod = AddPaymentInput.PaymentMethod,
                    Status = invoiceStatus,
                    ReferenceNumber = AddPaymentInput.ReferenceNumber
                };

                // Use transaction to ensure data integrity
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Invoices.Add(paymentInvoice);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                await _logSnagClient.PublishAsync(new LogSnagEvent
                {
                    Event = "Payment Recorded",
                    Description = $"{bill.Tenant.FullName} paid {AddPaymentInput.AmountPaid:C} for Bill #{bill.Id}",
                    Icon = "💰",
                    Tags = new Dictionary<string, string>
                    {
                        { "tenant", bill.Tenant.FullName },
                        { "billId", bill.Id.ToString() },
                        { "amount", AddPaymentInput.AmountPaid.ToString("0.##") }
                    }
                });

                SuccessMessage = $"Payment of {AddPaymentInput.AmountPaid:C} has been successfully recorded.";
                SelectedTenantId = AddPaymentInput.TenantId;
                return RedirectToPage(new { SelectedTenantId = AddPaymentInput.TenantId });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred while processing the payment: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnGetReceiptAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Tenant)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null || string.IsNullOrEmpty(invoice.ReceiptImagePath))
            {
                return NotFound();
            }

            var filePath = Path.Combine(_environment.WebRootPath, invoice.ReceiptImagePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = "image/jpeg";
            if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                contentType = "image/png";
            else if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                contentType = "application/pdf";

            return File(fileBytes, contentType);
        }

        // Helper Methods
        private async Task<PaymentSummaryViewModel> ComputePaymentSummaryAsync(int tenantId)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return new PaymentSummaryViewModel { TenantId = tenantId };
            }

            // Get all bills for this tenant
            var bills = await _context.Bills
                .Include(b => b.BillingPeriod)
                .Where(b => b.TenantId == tenantId)
                .ToListAsync();

            var billIds = bills.Select(b => b.Id).ToList();

            // Calculate actual paid amounts from invoices
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
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
                .Where(i => i.TenantId == tenantId && i.PaymentDate != null)
                .OrderByDescending(i => i.PaymentDate)
                .ToListAsync();

            var totalRent = bills.Sum(b => b.AmountDue);
            var totalPaid = bills.Sum(b => b.AmountPaid);
            var remainingBalance = totalRent - totalPaid;

            var lastPayment = paymentInvoices.FirstOrDefault();
            var latestPaymentMethod = lastPayment?.PaymentMethod ?? "N/A";

            var status = DetermineOverallStatus(totalRent, totalPaid, bills);

            return new PaymentSummaryViewModel
            {
                TenantId = tenantId,
                TenantName = tenant.FullName,
                UnitNumber = tenant.UnitNumber,
                TotalRent = totalRent,
                AmountPaid = totalPaid,
                RemainingBalance = remainingBalance,
                LastPaymentDate = lastPayment?.PaymentDate,
                LatestPaymentMethod = latestPaymentMethod,
                PaymentStatus = status
            };
        }

        private string DetermineOverallStatus(decimal totalRent, decimal totalPaid, List<Bill> bills)
        {
            if (totalPaid == 0)
                return "Unpaid";

            if (totalPaid >= totalRent)
                return "Paid";

            // Check for overdue bills
            var today = DateTime.Today;
            var hasOverdue = bills.Any(b => 
                today > b.DueDate && 
                (b.AmountDue - b.AmountPaid) > 0);

            if (hasOverdue)
                return "Overdue";

            return "Partial";
        }

        private string DetermineMonthlyStatus(decimal rentAmount, decimal paidAmount, DateTime dueDate)
        {
            var today = DateTime.Today;
            var remaining = rentAmount - paidAmount;

            if (paidAmount == 0)
            {
                if (today > dueDate)
                    return "Overdue";
                return "Unpaid";
            }

            if (paidAmount >= rentAmount)
            {
                if (paidAmount > rentAmount)
                    return "Advance";
                return "Paid";
            }

            if (today > dueDate && remaining > 0)
                return "Overdue";

            return "Partial";
        }

        private async Task LoadTenantSummariesAsync()
        {
            var query = _context.Tenants
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
                
                query = query.Where(t => 
                    t.FullName.Contains(sanitizedTerm) || 
                    t.UnitNumber.Contains(sanitizedTerm) ||
                    t.PrimaryEmail.Contains(sanitizedTerm));
            }

            var tenants = await query.ToListAsync();
            var tenantIds = tenants.Select(t => t.Id).ToList();

            // Get all bills for these tenants
            var allBills = await _context.Bills
                .Where(b => tenantIds.Contains(b.TenantId))
                .ToListAsync();

            var billIds = allBills.Select(b => b.Id).ToList();

            // Calculate actual paid amounts from invoices
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
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

            // Get last payments for each tenant
            var lastPayments = await _context.Invoices
                .Where(i => tenantIds.Contains(i.TenantId) && i.PaymentDate != null)
                .GroupBy(i => i.TenantId)
                .Select(g => new
                {
                    TenantId = g.Key,
                    LastPayment = g.OrderByDescending(i => i.PaymentDate).FirstOrDefault()
                })
                .ToDictionaryAsync(k => k.TenantId, v => v.LastPayment);

            TenantSummaries = tenants.Select(t =>
            {
                var bills = allBills.Where(b => b.TenantId == t.Id).ToList();
                
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

                lastPayments.TryGetValue(t.Id, out var lastPayment);

                return new TenantPaymentSummary
                {
                    TenantId = t.Id,
                    TenantName = t.FullName,
                    UnitNumber = t.UnitNumber,
                    MonthlyRent = t.MonthlyRent,
                    TotalPaid = totalPaidAllBills, // Total paid across ALL bills for accurate display
                    RemainingBalance = remainingBalance,
                    PaymentStatus = DetermineOverallStatus(totalRentAllBills, totalPaidAllBills, bills),
                    LastPaymentDate = lastPayment?.PaymentDate
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

        private async Task LoadMonthlyBreakdownAsync(int tenantId)
        {
            var bills = await _context.Bills
                .Include(b => b.BillingPeriod)
                .Where(b => b.TenantId == tenantId)
                .OrderByDescending(b => b.BillingPeriod.Year)
                .ThenByDescending(b => b.BillingPeriod.MonthName)
                .ToListAsync();

            var billIds = bills.Select(b => b.Id).ToList();

            // Calculate actual paid amounts from invoices
            var invoiceSums = await _context.Invoices
                .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
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
                    .Where(i => i.BillId == bill.Id && i.PaymentDate != null)
                    .OrderByDescending(i => i.PaymentDate)
                    .FirstOrDefaultAsync();

                var monthName = bill.BillingPeriod?.MonthName ?? "Unknown";
                var year = bill.BillingPeriod?.Year ?? DateTime.Now.Year;
                var status = DetermineMonthlyStatus(bill.AmountDue, actualPaid, bill.DueDate);

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
                    PaymentDate = latestPayment?.PaymentDate,
                    PaymentMethod = latestPayment?.PaymentMethod,
                    InvoiceId = latestPayment?.Id,
                    ReceiptImagePath = latestPayment?.ReceiptImagePath,
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
            // Tenant options
            var tenants = await _context.Tenants
                .OrderBy(t => t.FullName)
                .Select(t => new { t.Id, t.FullName, t.UnitNumber })
                .ToListAsync();

            TenantOptions = tenants.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = $"{t.FullName} - {t.UnitNumber}",
                Selected = SelectedTenantId == t.Id
            }).ToList();

            TenantOptions.Insert(0, new SelectListItem("All Tenants", "", !SelectedTenantId.HasValue));

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
