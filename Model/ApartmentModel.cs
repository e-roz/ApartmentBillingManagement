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
        public string UnitNumber { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Monthly Rent")]
        public decimal MonthlyRent { get; set; }

        [Display(Name = "Is Occupied")]
        public bool IsOccupied { get; set; }

        // Navigation properties
        public ICollection<Bill> Bills { get; set; } = new List<Bill>();
        public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    }
}
