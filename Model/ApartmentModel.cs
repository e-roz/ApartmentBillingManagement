using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class ApartmentModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Unit Number")]
        public string UnitNumber { get; set; }


        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Monthly  Rent")]
        public decimal MonthlyRent { get; set; }


        public bool IsOccupied { get; set; }



        // Foreign Key to the User (Tenant) who occupies this apartment
        public int? TenantId { get; set; }

        [ForeignKey("TenantId")]
        public User? Tenant { get; set; }

        public ICollection<Bill> Bills { get; set; } = new List<Bill>();

        }
}
