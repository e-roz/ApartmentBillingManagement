using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Apartment.Model;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Apartment.Services
{
    public class InvoicePdfService
    {
        private readonly ILogger<InvoicePdfService> _logger;
        private readonly IWebHostEnvironment _environment;
        private static readonly CultureInfo PhpCulture = CultureInfo.CreateSpecificCulture("en-PH");

        public InvoicePdfService(
            ILogger<InvoicePdfService> logger,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public byte[]? GenerateInvoicePdf(Invoice invoice)
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

                document.Add(CreateSummaryTable(invoice, labelFont, valueFont));

                AddSectionHeader(document, "Tenant Information", sectionFont);
                document.Add(CreateTwoColumnTable(new (string Label, string Value)[]
                {
                    ("Name", invoice.Tenant?.FullName ?? "Not available"),
                    ("Address", ResolveTenantAddress(invoice)),
                    ("Email", invoice.Tenant?.PrimaryEmail ?? "Not available"),
                    ("Phone", invoice.Tenant?.PrimaryPhone ?? "Not available")
                }, labelFont, valueFont));

                AddSectionHeader(document, "Apartment Details", sectionFont);
                document.Add(CreateTwoColumnTable(new[]
                {
                    ("Unit", ResolveApartmentUnit(invoice)),
                    ("Monthly Rent", FormatCurrency(invoice.Bill?.AmountDue ?? invoice.AmountDue))
                }, labelFont, valueFont));

                AddSectionHeader(document, "Bill Details", sectionFont);
                var bill = invoice.Bill;
                var description = ResolveBillDescription(invoice);
                var remaining = bill != null
                    ? Math.Max(0m, bill.AmountDue - bill.AmountPaid)
                    : 0m;

                document.Add(CreateTwoColumnTable(new[]
                {
                    ("Description", description),
                    ("Status", invoice.Status.ToString()),
                    ("Amount Due", FormatCurrency(bill?.AmountDue ?? invoice.AmountDue)),
                    ("Amount Paid (this invoice)", FormatCurrency(invoice.AmountDue)),
                    ("Remaining Balance", FormatCurrency(remaining))
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

        private static PdfPTable CreateSummaryTable(Invoice invoice, Font labelFont, Font valueFont)
        {
            var left = new PdfPTable(1) { WidthPercentage = 100 };
            AddKeyValueRow(left, "Invoice #", invoice.Id.ToString(), labelFont, valueFont);
            AddKeyValueRow(left, "Invoice Date", FormatDate(invoice.IssueDate), labelFont, valueFont);
            AddKeyValueRow(left, "Due Date", FormatDate(invoice.DueDate), labelFont, valueFont);

            var right = new PdfPTable(1) { WidthPercentage = 100 };
            AddKeyValueRow(right, "Status", invoice.Status.ToString(), labelFont, valueFont);
            AddKeyValueRow(right, "Tenant", invoice.Tenant?.FullName ?? "Not available", labelFont, valueFont);
            AddKeyValueRow(right, "Bill Reference", ResolveBillReference(invoice), labelFont, valueFont);

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
            if (!string.IsNullOrWhiteSpace(invoice.Tenant?.UnitNumber))
            {
                return $"Unit {invoice.Tenant.UnitNumber}";
            }

            if (!string.IsNullOrWhiteSpace(invoice.Apartment?.UnitNumber))
            {
                return $"Apartment {invoice.Apartment.UnitNumber}";
            }

            return "Not available";
        }

        private static string ResolveApartmentUnit(Invoice invoice)
        {
            return invoice.Apartment?.UnitNumber
                ?? invoice.Bill?.Apartment?.UnitNumber
                ?? "Not available";
        }

        private static string ResolveBillReference(Invoice invoice)
        {
            if (invoice.Bill == null)
            {
                return invoice.Title ?? "N/A";
            }

            var period = invoice.Bill.BillingPeriod;
            if (period != null)
            {
                return $"Bill #{invoice.Bill.Id} - {period.MonthName} {period.Year}";
            }

            return $"Bill #{invoice.Bill.Id}";
        }

        private static string ResolveBillDescription(Invoice invoice)
        {
            var bill = invoice.Bill;
            if (bill?.BillingPeriod != null)
            {
                return $"{bill.BillingPeriod.MonthName} {bill.BillingPeriod.Year} Rent";
            }

            return invoice.Title ?? "Rent Payment";
        }

        private static string FormatDate(DateTime date) =>
            date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);

        private static string FormatCurrency(decimal amount) =>
            amount.ToString("C", PhpCulture);

    }
}

