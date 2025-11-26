using Apartment.Enums;
using System.Threading.Tasks;

namespace Apartment.Services
{
    public interface IAuditService
    {
        Task LogAsync(AuditActionType action, int? userId, string details, int? entityId = null, string? entityType = null, bool success = true);
    }
}
