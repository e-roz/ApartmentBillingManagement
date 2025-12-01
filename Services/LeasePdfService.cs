using Apartment.Model;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Apartment.Services
{
    public class LeasePdfService
    {
        private readonly ILogger<LeasePdfService> _logger;
        // Define colors to avoid repeated instantiation and for consistency
        private static readonly BaseColor Black = new BaseColor(0, 0, 0);
        private static readonly BaseColor White = new BaseColor(255, 255, 255);
        private static readonly BaseColor LightGray = new BaseColor(240, 240, 240);
        private static readonly BaseColor DarkGreen = new BaseColor(74, 111, 71);
        private static readonly BaseColor BorderColor = new BaseColor(231, 229, 216);

        public LeasePdfService(ILogger<LeasePdfService> logger)
        {
            _logger = logger;
        }

        public byte[]? GenerateLeasesPdf(IEnumerable<Lease> leases)
        {
            if (leases == null || !leases.Any())
            {
                _logger.LogWarning("No leases provided for PDF generation.");
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                var document = new Document(PageSize.A4.Rotate(), 20, 20, 20, 20);
                try
                {
                    PdfWriter.GetInstance(document, memoryStream);
                    document.Open();

                    // Fonts
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, Black);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, White);
                    var subHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, Black);
                    var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, Black);

                    document.Add(new Paragraph("Apartment Lease Summary", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 10 });

                    foreach (var lease in leases.OrderBy(l => l.UnitNumber).ThenBy(l => l.LeaseStart))
                    {
                        document.Add(new Paragraph($"Lease for Unit: {lease.UnitNumber} - Tenant: {lease.User?.Username ?? "N/A"}", subHeaderFont) { SpacingBefore = 15, SpacingAfter = 5 });

                        var table = new PdfPTable(8) { WidthPercentage = 100 };
                        table.SetWidths(new float[] { 2f, 1f, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f, 1f });

                        // Table Headers
                        AddCellToTable(table, "Tenant", headerFont, DarkGreen);
                        AddCellToTable(table, "Unit", headerFont, DarkGreen);
                        AddCellToTable(table, "Start Date", headerFont, DarkGreen);
                        AddCellToTable(table, "End Date", headerFont, DarkGreen);
                        AddCellToTable(table, "Monthly Rent", headerFont, DarkGreen, Element.ALIGN_RIGHT);
                        AddCellToTable(table, "Security Deposit", headerFont, DarkGreen, Element.ALIGN_RIGHT);
                        AddCellToTable(table, "Late Fee", headerFont, DarkGreen, Element.ALIGN_RIGHT);
                        AddCellToTable(table, "Pets", headerFont, DarkGreen, Element.ALIGN_CENTER);

                        // Table Data
                        AddCellToTable(table, lease.User?.Username ?? "N/A", cellFont);
                        AddCellToTable(table, lease.UnitNumber ?? "N/A", cellFont);
                        AddCellToTable(table, lease.LeaseStart.ToString("yyyy-MM-dd"), cellFont);
                        AddCellToTable(table, lease.LeaseEnd.ToString("yyyy-MM-dd"), cellFont);
                        AddCellToTable(table, lease.MonthlyRent.ToString("C", new CultureInfo("en-PH")), cellFont, null, Element.ALIGN_RIGHT);
                        AddCellToTable(table, lease.SecurityDeposit.ToString("C", new CultureInfo("en-PH")), cellFont, null, Element.ALIGN_RIGHT);
                        AddCellToTable(table, lease.LateFeeAmount.ToString("C", new CultureInfo("en-PH")), cellFont, null, Element.ALIGN_RIGHT);
                        AddCellToTable(table, lease.PetsAllowed ? "Yes" : "No", cellFont, null, Element.ALIGN_CENTER);

                        document.Add(table);
                    }

                    document.Close();
                    return memoryStream.ToArray();
                }
                catch (DocumentException dex)
                {
                    _logger.LogError(dex, "Document error while generating lease PDF.");
                    return null;
                }
                catch (IOException ioex)
                {
                    _logger.LogError(ioex, "File access error while generating lease PDF.");
                    return null;
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate lease PDF.");
                    return null;
                }
            }
        }

        private void AddCellToTable(PdfPTable table, string text, Font font, BaseColor? bgColor = null, int horizontalAlignment = Element.ALIGN_LEFT)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            {
                Padding = 5,
                BackgroundColor = bgColor ?? White,
                BorderColor = BorderColor,
                BorderWidth = 0.5f,
                HorizontalAlignment = horizontalAlignment,
                VerticalAlignment = Element.ALIGN_MIDDLE
            };
            table.AddCell(cell);
        }
    }
}
