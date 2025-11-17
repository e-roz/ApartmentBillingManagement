using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Apartment.Enums;
namespace Apartment.Model
{
    public class Tenant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }


        [Required]
        [StringLength(100)]
        public string PrimaryEmail { get; set; }



        [Required]
        [StringLength(20)]
        public string PrimaryPhone { get; set; }

        // lease and unit information
        [Required]
        public DateTime LeaseStartDate { get; set; }
        public DateTime LeaseEndDate { get; set; }


        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal MonthlyRent { get; set; }

        public LeaseStatus Status { get; set; } = LeaseStatus.Prospective;

        public int? ApartmentId { get; set; }

        [ForeignKey(nameof(ApartmentId))]
        public ApartmentModel? Apartment { get; set; }

        [StringLength(10)]
        public string UnitNumber { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public User? UserAccount { get; set; }

        public ICollection<Bill> Bills { get; set; } = new List<Bill>();

    }
}
