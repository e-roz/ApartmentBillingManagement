using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "User")]
    public class ViewInvoicesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewInvoicesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Invoice> Invoices { get; set; } = new List<Invoice>();
        public Model.Tenant? TenantInfo { get; set; }

        public async Task OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                var user = await _context.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user != null)
                {
                    // First try to get tenant from User.TenantID (direct relationship)
                    if (user.Tenant != null)
                    {
                        TenantInfo = user.Tenant;
                    }
                    else
                    {
                        // Fallback: Try to find tenant via TenantLink using email
                        var tenantLink = await _context.TenantLinks
                            .FirstOrDefaultAsync(tl => tl.UserId == userId.ToString());

                        if (tenantLink != null && int.TryParse(tenantLink.ApartmentId, out int apartmentId))
                        {
                            // Find tenant by apartment
                            var tenant = await _context.Tenants
                                .FirstOrDefaultAsync(t => t.ApartmentId == apartmentId && t.PrimaryEmail == user.Email);
                            
                            if (tenant != null)
                            {
                                TenantInfo = tenant;
                                // Update User.TenantID for future use
                                user.TenantID = tenant.Id;
                                _context.Users.Update(user);
                                await _context.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            // Last resort: Find tenant by email match
                            var tenantByEmail = await _context.Tenants
                                .FirstOrDefaultAsync(t => t.PrimaryEmail == user.Email);
                            
                            if (tenantByEmail != null)
                            {
                                TenantInfo = tenantByEmail;
                                // Update User.TenantID for future use
                                user.TenantID = tenantByEmail.Id;
                                _context.Users.Update(user);
                                await _context.SaveChangesAsync();
                            }
                        }
                    }

                    // Fetch all Invoice entities where TenantId matches the logged-in tenant's ID
                    if (TenantInfo != null)
                    {
                        var tenantId = TenantInfo.Id;
                        Invoices = await _context.Invoices
                            .Include(i => i.Bill)
                                .ThenInclude(b => b.BillingPeriod)
                            .Include(i => i.Apartment)
                            .Where(i => i.TenantId == tenantId)
                            .OrderByDescending(i => i.IssueDate)
                            .ToListAsync();
                    }
                }
            }
        }
    }
}

