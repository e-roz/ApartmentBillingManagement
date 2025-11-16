using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
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
        public string Username { get; set; }

        // Stores the securely HASHED password (never plain text)
        [Required]
        [StringLength(255)]
        public string HasedPassword { get; set; } = string.Empty;

        // Uses the UserRole enum. Defaults to the lowest level (User)
        public UserRoles Role { get; set; } = UserRoles.User;

        [Required]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;



        public int? TenantID { get; set; }

        [ForeignKey(nameof(TenantID))]
        public Tenant? Tenant { get; set; }

    }

}
