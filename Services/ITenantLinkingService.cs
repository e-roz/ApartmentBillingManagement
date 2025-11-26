using Apartment.Model;
using System.Threading.Tasks;
namespace Apartment.Services
{
    public interface ITenantLinkingService
    {


        Task<bool> LinkTenantToUserAsync(User user);

        Task LinkTenantToUserAsync(string userId, string apartmentId);
    }
}
