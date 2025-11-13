using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace Apartment.Model
{
    // This model maps directly to the Users table in the database
    [Index(nameof(Username), IsUnique = true)] // Ensure usernames are unique at the DB level
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
        public string HasedPassword { get; set; }

        // Uses the UserRole enum. Defaults to the lowest level (User)
        public UserRoles Role { get; set; } = UserRoles.User;

        [Required]
        public string Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;



        public int? TenantID { get; set; }

        [ForeignKey("TenantID")]
        public Tenant? Tenant { get; set; }

    }

}
