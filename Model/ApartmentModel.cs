using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    [Index(nameof(UnitNumber), IsUnique = true)]
    public class ApartmentModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        [Display(Name = "Unit Number")]
        public string UnitNumber { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Monthly Rent")]
        public decimal MonthlyRent { get; set; }

        [Display(Name = "Is Occupied")]
        public bool IsOccupied { get; set; }

        // Foreign Key to the User (Tenant) who occupies this apartment
        // Note: This should reference Users.Id, but keeping TenantId name for now
        // TODO: Update FK to point to Users table in future migration
        public int? TenantId { get; set; }

        [ForeignKey("TenantId")]
        public User? CurrentTenant { get; set; }

        public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    }
}
