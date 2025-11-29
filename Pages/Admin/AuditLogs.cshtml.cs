using Apartment.Data;
using Apartment.Model;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AuditLogsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ExcelExportService _excelExportService;
        private readonly AuditLogPdfService _pdfService;

        public AuditLogsModel(ApplicationDbContext context, ExcelExportService excelExportService, AuditLogPdfService pdfService)
        {
            _context = context;
            _excelExportService = excelExportService;
            _pdfService = pdfService;
        }

        public IList<AuditLog>? AuditLogs { get; set; }

        public async Task OnGetAsync()
        {
            AuditLogs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(200) // Limit initial load for performance
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostExportExcelAsync()
        {
            var auditLogs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            using (var workbook = _excelExportService.BuildAuditLogWorkbook(auditLogs))
            {
                var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                Response.Cookies.Append("fileDownload", "true", new CookieOptions { Path = "/" });
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AuditLogs.xlsx");
            }
        }

        public async Task<IActionResult> OnPostExportPdfAsync()
        {
            var auditLogs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        
            var pdfBytes = await _pdfService.GenerateAuditLogPdfAsync(auditLogs);
            Response.Cookies.Append("fileDownload", "true", new CookieOptions { Path = "/" });
            return File(pdfBytes, "application/pdf", "AuditLogs.pdf");
        }
    }
}