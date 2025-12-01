using Apartment.Data;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class InvoiceDownloadModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly InvoicePdfService _invoicePdfService;
        private readonly ILogger<InvoiceDownloadModel> _logger;

        public InvoiceDownloadModel(
            ApplicationDbContext context,
            InvoicePdfService invoicePdfService,
            ILogger<InvoiceDownloadModel> logger)
        {
            _context = context;
            _invoicePdfService = invoicePdfService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(int invoiceId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.TenantUser)
                .Include(i => i.Apartment)
                .Include(i => i.Bill!)
                    .ThenInclude(b => b.BillingPeriod)
                .Include(i => i.Bill!)
                    .ThenInclude(b => b.Apartment)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantUserId == userId);

            if (invoice == null)
            {
                return NotFound();
            }

            byte[] pdfBytes;
            try
            {
                pdfBytes = await _invoicePdfService.GenerateInvoicePdfAsync(invoice) ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to generate PDF for InvoiceId {InvoiceId}", invoiceId);
                return StatusCode(500, "Unable to generate invoice PDF at this time.");
            }

            if (pdfBytes.Length == 0)
            {
                _logger.LogWarning("Failed to generate PDF for InvoiceId {InvoiceId}", invoiceId);
                return StatusCode(500, "Unable to generate invoice PDF at this time.");
            }

            var fileName = $"invoice_{invoice.Id}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}

