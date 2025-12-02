using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Apartment.Data;
using Apartment.Model;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apartment.Services
{
    public class InvoicePdfService
    {
        private readonly ILogger<InvoicePdfService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _context;
        private static readonly CultureInfo PhpCulture = CultureInfo.CreateSpecificCulture("en-PH");

        public InvoicePdfService(
            ILogger<InvoicePdfService> logger,
            IWebHostEnvironment environment,
            ApplicationDbContext context)
        {
            _logger = logger;
            _environment = environment;
            _context = context;
        }

        public async Task<byte[]?> GenerateInvoicePdfAsync(Invoice invoice)
        {
            if (invoice == null)
            {
                return null;
            }

            try
            {
                using var memoryStream = new MemoryStream();
                var document = new Document(PageSize.A4, 36, 36, 36, 36);
                PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20);
                var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 13);
                var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11);
                var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);

                AddLogo(document);

                var title = new Paragraph("Statement of Payment", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 12
                };
                document.Add(title);

                document.Add(await CreateSummaryTableAsync(invoice, labelFont, valueFont));

                AddSectionHeader(document, "Tenant Information", sectionFont);
                document.Add(CreateTwoColumnTable(new (string Label, string Value)[]
                {
                    ("Name", invoice.TenantUser?.Username ?? "Not available"),
                    ("Address", ResolveTenantAddress(invoice)),
                    ("Email", invoice.TenantUser?.Email ?? "Not available"),
                    ("Phone", "Not available")
                }, labelFont, valueFont));

                AddSectionHeader(document, "Apartment Details", sectionFont);
                var monthlyRent = await ResolveMonthlyRentAsync(invoice);
                document.Add(CreateTwoColumnTable(new[]
                {
                    ("Unit", ResolveApartmentUnit(invoice)),
                    ("Monthly Rent", FormatCurrency(monthlyRent))
                }, labelFont, valueFont));

                AddSectionHeader(document, "Bill Details", sectionFont);
                var bill = invoice.Bill;
                var description = await ResolveBillDescriptionAsync(invoice);
                
                // Calculate remaining balance using PaymentAllocations (accurate source of truth)
                decimal remaining = 0m;
                if (bill != null)
                {
                    // Single bill payment - calculate remaining for this specific bill
                    var totalPaidOnBill = await _context.PaymentAllocations
                        .Where(pa => pa.BillId == bill.Id)
                        .SumAsync(pa => pa.AmountApplied);
                    remaining = Math.Max(0m, bill.AmountDue - totalPaidOnBill);
                }
                else
                {
                    // Outstanding balance payment - calculate remaining for all bills in this invoice's allocations
                    var billIdsInThisInvoice = await _context.PaymentAllocations
                        .Where(pa => pa.InvoiceId == invoice.Id)
                        .Select(pa => pa.BillId)
                        .Distinct()
                        .ToListAsync();
                    
                    if (billIdsInThisInvoice.Any())
                    {
                        var billsInInvoice = await _context.Bills
                            .Where(b => billIdsInThisInvoice.Contains(b.Id))
                            .ToListAsync();
                        
                        var paymentAllocationSums = await _context.PaymentAllocations
                            .Where(pa => billIdsInThisInvoice.Contains(pa.BillId))
                            .GroupBy(pa => pa.BillId)
                            .Select(group => new
                            {
                                BillId = group.Key,
                                TotalPaid = group.Sum(pa => pa.AmountApplied)
                            })
                            .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);
                        
                        remaining = billsInInvoice
                            .Sum(b =>
                            {
                                var paidAmount = paymentAllocationSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                                return Math.Max(0m, b.AmountDue - paidAmount);
                            });
                    }
                }

                // Calculate overall remaining balance across all bills for this tenant using PaymentAllocations
                decimal overallRemainingBalance = 0m;
                if (invoice.TenantUserId > 0)
                {
                    try
                    {
                        var allBills = await _context.Bills
                            .Include(b => b.BillingPeriod)
                            .Where(b => b.TenantUserId == invoice.TenantUserId)
                            .ToListAsync();

                        var allBillIds = allBills.Select(b => b.Id).ToList();
                        
                        if (allBillIds.Any())
                        {
                            // Calculate total paid per bill using PaymentAllocations (accurate for all payment types)
                            var paymentAllocationSums = await _context.PaymentAllocations
                                .Where(pa => allBillIds.Contains(pa.BillId))
                                .GroupBy(pa => pa.BillId)
                                .Select(group => new
                                {
                                    BillId = group.Key,
                                    TotalPaid = group.Sum(pa => pa.AmountApplied)
                                })
                                .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

                            overallRemainingBalance = allBills
                                .Sum(b =>
                                {
                                    var paidAmount = paymentAllocationSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                                    return Math.Max(0m, b.AmountDue - paidAmount);
                                });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to calculate overall remaining balance for tenant user {TenantUserId}", invoice.TenantUserId);
                    }
                }

                document.Add(CreateTwoColumnTable(new[]
                {
                    ("Description", description),
                    ("Status", invoice.Status.ToString()),
                    ("Amount Due", FormatCurrency(bill?.AmountDue ?? invoice.AmountDue)),
                    ("Amount Paid (this invoice)", FormatCurrency(invoice.AmountDue)),
                    ("Remaining Balance (this bill)", FormatCurrency(remaining)),
                    ("Overall Remaining Balance", FormatCurrency(overallRemainingBalance))
                }, labelFont, valueFont));

                document.Close();
                return memoryStream.ToArray();
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, "File access error while generating invoice PDF for InvoiceId {InvoiceId}", invoice?.Id);
                throw new InvalidOperationException("Unable to access required files while generating the invoice.", ioEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate invoice PDF for InvoiceId {InvoiceId}", invoice?.Id);
                throw new InvalidOperationException("An unexpected error occurred while generating the invoice.", ex);
            }
        }

        private void AddLogo(Document document)
        {
            try
            {
                var logoPath = Path.Combine(_environment.WebRootPath ?? string.Empty, "images", "Logo.png");
                if (!File.Exists(logoPath))
                {
                    _logger.LogWarning("Invoice logo not found at {LogoPath}", logoPath);
                    return;
                }

                var logo = Image.GetInstance(logoPath);

                logo.ScaleToFit(200f, 120f);

                logo.Alignment = Image.ALIGN_LEFT;
                logo.SpacingAfter = 10f;

                document.Add(logo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load invoice logo.");
            }
        }

        private static void AddSectionHeader(Document document, string text, Font font)
        {
            var header = new Paragraph(text, font)
            {
                SpacingBefore = 15f,
                SpacingAfter = 6f
            };
            document.Add(header);
        }

        private async Task<PdfPTable> CreateSummaryTableAsync(Invoice invoice, Font labelFont, Font valueFont)
        {
            // For payment invoices, use DateFullySettled as invoice date and due date
            var invoiceDate = invoice.DateFullySettled ?? invoice.IssueDate;
            var dueDate = invoice.DateFullySettled ?? invoice.DueDate;

            var left = new PdfPTable(1) { WidthPercentage = 100 };
            AddKeyValueRow(left, "Invoice #", invoice.Id.ToString(), labelFont, valueFont);
            AddKeyValueRow(left, "Invoice Date", FormatDate(invoiceDate), labelFont, valueFont);
            AddKeyValueRow(left, "Due Date", FormatDate(dueDate), labelFont, valueFont);

            var right = new PdfPTable(1) { WidthPercentage = 100 };
            AddKeyValueRow(right, "Status", invoice.Status.ToString(), labelFont, valueFont);
            AddKeyValueRow(right, "Tenant", invoice.TenantUser?.Username ?? "Not available", labelFont, valueFont);
            AddKeyValueRow(right, "Bill Reference", await ResolveBillReferenceAsync(invoice), labelFont, valueFont);

            var container = new PdfPTable(2) { WidthPercentage = 100 };
            container.SetWidths(new[] { 1f, 1f });
            container.AddCell(new PdfPCell(left) { Border = Rectangle.NO_BORDER });
            container.AddCell(new PdfPCell(right) { Border = Rectangle.NO_BORDER });
            return container;
        }

        private static void AddKeyValueRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var phrase = new Phrase();
            phrase.Add(new Chunk($"{label}: ", labelFont));
            phrase.Add(new Chunk(value, valueFont));

            table.AddCell(new PdfPCell(phrase)
            {
                Border = Rectangle.NO_BORDER,
                PaddingBottom = 4f
            });
        }

        private static PdfPTable CreateTwoColumnTable(IEnumerable<(string Label, string Value)> rows, Font labelFont, Font valueFont)
        {
            var table = new PdfPTable(2) { WidthPercentage = 100, SpacingBefore = 4f };
            table.SetWidths(new[] { 1f, 1f });

            foreach (var (label, value) in rows)
            {
                table.AddCell(CreateLabelCell(label, labelFont));
                table.AddCell(CreateValueCell(value, valueFont));
            }

            return table;
        }

        private static PdfPCell CreateLabelCell(string text, Font font) =>
            new PdfPCell(new Phrase(text, font))
            {
                BackgroundColor = new BaseColor(245, 245, 245),
                Padding = 8
            };

        private static PdfPCell CreateValueCell(string text, Font font) =>
            new PdfPCell(new Phrase(text, font)) { Padding = 8 };

        private static string ResolveTenantAddress(Invoice invoice)
        {
            if (!string.IsNullOrWhiteSpace(invoice.Apartment?.UnitNumber))
            {
                return $"Apartment {invoice.Apartment.UnitNumber}";
            }

            // Try to get unit number from Bill's Apartment
            if (invoice.Bill?.Apartment != null && !string.IsNullOrWhiteSpace(invoice.Bill.Apartment.UnitNumber))
            {
                return $"Apartment {invoice.Bill.Apartment.UnitNumber}";
            }

            return "Not available";
        }

        private static string ResolveApartmentUnit(Invoice invoice)
        {
            return invoice.Apartment?.UnitNumber
                ?? invoice.Bill?.Apartment?.UnitNumber
                ?? "Not available";
        }

        private async Task<string> ResolveBillReferenceAsync(Invoice invoice)
        {
            // Check PaymentAllocations to determine if this is outstanding balance or single bill payment
            var allocations = await _context.PaymentAllocations
                .Include(pa => pa.Bill)
                    .ThenInclude(b => b.BillingPeriod)
                .Where(pa => pa.InvoiceId == invoice.Id)
                .OrderBy(pa => pa.Bill.DueDate)
                .ToListAsync();

            if (allocations.Count == 0)
            {
                // No allocations found, fall back to invoice.Bill or title
                if (invoice.Bill != null)
                {
                    var period = invoice.Bill.BillingPeriod;
                    if (period != null)
                    {
                        return $"{period.MonthName} {period.Year}";
                    }
                    return $"Bill #{invoice.Bill.Id}";
                }
                return invoice.Title ?? "N/A";
            }
            else if (allocations.Count == 1)
            {
                // Single bill payment - return month name
                var bill = allocations[0].Bill;
                if (bill.BillingPeriod != null)
                {
                    return $"{bill.BillingPeriod.MonthName} {bill.BillingPeriod.Year}";
                }
                return $"Bill #{bill.Id}";
            }
            else
            {
                // Outstanding balance payment - return month range
                var firstBill = allocations.First().Bill;
                var lastBill = allocations.Last().Bill;
                
                var firstPeriod = firstBill.BillingPeriod;
                var lastPeriod = lastBill.BillingPeriod;
                
                if (firstPeriod != null && lastPeriod != null)
                {
                    if (firstPeriod.Year == lastPeriod.Year && firstPeriod.MonthName == lastPeriod.MonthName)
                    {
                        return $"Outstanding Balance - {firstPeriod.MonthName} {firstPeriod.Year}";
                    }
                    return $"Outstanding Balance between {firstPeriod.MonthName} {firstPeriod.Year} and {lastPeriod.MonthName} {lastPeriod.Year}";
                }
                return "Outstanding Balance";
            }
        }

        private async Task<string> ResolveBillDescriptionAsync(Invoice invoice)
        {
            // Check PaymentAllocations to determine description
            var allocations = await _context.PaymentAllocations
                .Include(pa => pa.Bill)
                    .ThenInclude(b => b.BillingPeriod)
                .Where(pa => pa.InvoiceId == invoice.Id)
                .OrderBy(pa => pa.Bill.DueDate)
                .ToListAsync();

            if (allocations.Count == 0)
            {
                // No allocations, fall back to invoice.Bill or title
                var bill = invoice.Bill;
                if (bill?.BillingPeriod != null)
                {
                    return $"{bill.BillingPeriod.MonthName} {bill.BillingPeriod.Year} Rent";
                }
                return invoice.Title ?? "Rent Payment";
            }
            else if (allocations.Count == 1)
            {
                // Single bill payment
                var bill = allocations[0].Bill;
                if (bill.BillingPeriod != null)
                {
                    return $"{bill.BillingPeriod.MonthName} {bill.BillingPeriod.Year} Rent";
                }
                return "Rent Payment";
            }
            else
            {
                // Outstanding balance payment
                return "Outstanding Balance Payment";
            }
        }

        private async Task<decimal> ResolveMonthlyRentAsync(Invoice invoice)
        {
            // If invoice has a direct Bill reference, use that bill's AmountDue
            if (invoice.Bill != null)
            {
                return invoice.Bill.AmountDue;
            }

            // For outstanding balance payments, get the monthly rent from the first bill in PaymentAllocations
            var allocations = await _context.PaymentAllocations
                .Include(pa => pa.Bill)
                .Where(pa => pa.InvoiceId == invoice.Id)
                .OrderBy(pa => pa.Bill.DueDate)
                .FirstOrDefaultAsync();

            if (allocations != null)
            {
                return allocations.Bill.AmountDue;
            }

            // Fallback: try to get monthly rent from lease if available
            // This is a last resort if no allocations exist
            return invoice.AmountDue;
        }

        private static string FormatDate(DateTime date) =>
            date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);

        private static string FormatCurrency(decimal amount) =>
            amount.ToString("C", PhpCulture);

    }
}

