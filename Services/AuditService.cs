using Apartment.Data;
using Apartment.Enums;
using Apartment.Model;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Apartment.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(AuditActionType action, int? userId, string details, int? entityId = null, string? entityType = null, bool success = true)
        {
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            var auditLog = new AuditLog
            {
                Action = action,
                UserId = userId,
                Details = details,
                EntityId = entityId,
                EntityType = entityType,
                Success = success,
                IpAddress = ipAddress,
                Timestamp = System.DateTime.UtcNow
            };

            await _context.AuditLogs.AddAsync(auditLog);
            // We assume that SaveChangesAsync will be called by the parent operation's DbContext.
            // This allows audit logs to be part of the same transaction.
        }
    }
}
