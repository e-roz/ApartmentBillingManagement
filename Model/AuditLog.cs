using Apartment.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        // The user who performed the action. Can be null for system actions.
        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        public AuditActionType Action { get; set; }

        [Required]
        public string Details { get; set; } = string.Empty;

        // The ID of the entity that was affected (e.g., the ID of the user that was deleted).
        public int? EntityId { get; set; }
        public string? EntityType { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(45)] // Suitable for IPv6 addresses
        public string? IpAddress { get; set; }

        public bool Success { get; set; } = true;
    }
}
