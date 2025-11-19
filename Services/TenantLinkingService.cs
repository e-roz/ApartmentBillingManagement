using Apartment.Data;
using Apartment.Model;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Apartment.Services
{
    public class TenantLinkingService : ITenantLinkingService
    {
      private readonly ApplicationDbContext _context;

        public TenantLinkingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> LinkTenantToUserAsync(User user)
        {
            if(user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            // find the tenant record by matching the email (CASE HINDI MAKARAMDAM)
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.PrimaryEmail.ToLower() == user.Email.ToLower());
            if(tenant == null)
            {

                //no existing tenant record
                return false;
            }

            // link user to the found tenant by setting the foreign key
            user.TenantID = tenant.Id;

            // mark the user entity as modified and the change is saved
            _context.Users.Update(user);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task LinkTenantToUserAsync(string userId, string apartmentId)
        {
            var tenantLink = new TenantLink
            {
                UserId = userId,
                ApartmentId = apartmentId,
                LinkedDate = DateTime.UtcNow
            };

            _context.TenantLinks.Add(tenantLink);
            await _context.SaveChangesAsync();
        }

    }
}
