using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Apartment.Enums;
namespace Apartment.Model
{
    // This model maps directly to the Users table in the database
    // Note: Username is NOT unique - it's for display purposes only
    [Index(nameof(Email), IsUnique = true)] // Ensure emails are unique at the DB level (used for login)
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        // Stores the securely HASHED password (never plain text)
        [Required]
        [StringLength(255)]
        public string HasedPassword { get; set; } = string.Empty;

        // Uses the UserRole enum. Defaults to Tenant
        public UserRoles Role { get; set; } = UserRoles.Tenant;

        [Required]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        // Tenant properties merged from Tenants table
        public int? ApartmentId { get; set; }

        public DateTime? LeaseStart { get; set; }

        public DateTime? LeaseEnd { get; set; }

        [StringLength(10)]
        public string? UnitNumber { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MonthlyRent { get; set; }

        [StringLength(32)]
        public string? Status { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ApartmentId))]
        public ApartmentModel? Apartment { get; set; }

        public ICollection<Bill>? Bills { get; set; } // Added for User-Bills relationship

        public bool MustChangePassword { get; set; } = false;
    }

}
