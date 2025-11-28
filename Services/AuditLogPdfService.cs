using Apartment.Model;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Apartment.Services
{
    public class AuditLogPdfService
    {
        private readonly ILogger<AuditLogPdfService> _logger;

        public AuditLogPdfService(ILogger<AuditLogPdfService> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> GenerateAuditLogPdfAsync(IEnumerable<AuditLog> auditLogs)
        {
            using var memoryStream = new MemoryStream();
            var document = new Document(PageSize.A4.Rotate(), 36, 36, 36, 36); // Landscape
            PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            var title = new Paragraph("Audit Log Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            var table = new PdfPTable(6) { WidthPercentage = 100 };
            table.SetWidths(new[] { 3f, 2f, 2f, 4f, 2f, 1.5f });

            var lightGray = new BaseColor(211, 211, 211);
            table.AddCell(new PdfPCell(new Phrase("Timestamp", headerFont)) { BackgroundColor = lightGray });
            table.AddCell(new PdfPCell(new Phrase("User", headerFont)) { BackgroundColor = lightGray });
            table.AddCell(new PdfPCell(new Phrase("Action", headerFont)) { BackgroundColor = lightGray });
            table.AddCell(new PdfPCell(new Phrase("Details", headerFont)) { BackgroundColor = lightGray });
            table.AddCell(new PdfPCell(new Phrase("IP Address", headerFont)) { BackgroundColor = lightGray });
            table.AddCell(new PdfPCell(new Phrase("Success", headerFont)) { BackgroundColor = lightGray });

            foreach (var log in auditLogs)
            {
                table.AddCell(new PdfPCell(new Phrase(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), cellFont)));
                table.AddCell(new PdfPCell(new Phrase(log.User?.Username ?? "System", cellFont)));
                table.AddCell(new PdfPCell(new Phrase(log.Action.ToString(), cellFont)));
                table.AddCell(new PdfPCell(new Phrase(log.Details, cellFont)));
                table.AddCell(new PdfPCell(new Phrase(log.IpAddress ?? "N/A", cellFont)));
                table.AddCell(new PdfPCell(new Phrase(log.Success ? "Yes" : "No", cellFont)));
            }

            document.Add(table);
            document.Close();

            return await Task.FromResult(memoryStream.ToArray());
        }
    }
}
