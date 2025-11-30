using System.Threading.Tasks;

namespace Apartment.Services
{
    public interface IEmailService
    {
        Task SendNewAccountEmailAsync(string email, string username, string temporaryPassword);
        Task SendPasswordResetEmailAsync(string email, string username, string temporaryPassword);
    }
}
