using Apartment.Data;
using Apartment.Model;
using Apartment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class DocumentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LeasePdfService _leasePdfService;

        public DocumentsModel(ApplicationDbContext context, LeasePdfService leasePdfService)
        {
            _context = context;
            _leasePdfService = leasePdfService;
        }

        public Model.User? UserInfo { get; set; }
        public Lease? ActiveLease { get; set; }

        public async Task OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Leases)
                        .ThenInclude(l => l.Apartment)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    UserInfo = user;
                    var now = DateTime.UtcNow;
                    ActiveLease = user.Leases?.FirstOrDefault(l => l.LeaseEnd >= now);
                }
            }
        }

        public async Task<IActionResult> OnPostDownloadLeaseAsync(int leaseId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized();
            }

            var lease = await _context.Leases
                .Include(l => l.User)
                .Include(l => l.Apartment)
                .FirstOrDefaultAsync(l => l.Id == leaseId && l.UserId == userId);

            if (lease == null)
            {
                return NotFound();
            }

            var pdfBytes = _leasePdfService.GenerateLeasesPdf(new[] { lease });
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                // If generation fails, go back to page (could be enhanced with a message later)
                return RedirectToPage();
            }

            var fileName = $"Lease_{lease.UnitNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}

