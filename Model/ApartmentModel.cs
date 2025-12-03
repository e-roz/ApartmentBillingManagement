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

        [Display(Name = "Is Occupied")]
        public bool IsOccupied { get; set; }

        [Required]
        [Display(Name = "Apartment Type")]
        [Column(TypeName = "nvarchar(24)")]
        public Enums.ApartmentType ApartmentType { get; set; } = Enums.ApartmentType.Studio;

        // Navigation properties
        public ICollection<Bill> Bills { get; set; } = new List<Bill>();
        public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    }
}
