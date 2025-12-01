using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class Lease
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to link lease to a specific user (tenant)
        [Required]
        public int UserId { get; set; }

        // Foreign Key to link lease to a specific apartment
        [Required]
        public int ApartmentId { get; set; }

        // Lease start date
        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime LeaseStart { get; set; }

        // Lease end date
        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime LeaseEnd { get; set; }

        // Monthly rent amount
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal MonthlyRent { get; set; }

        // Unit number for this lease
        [Required]
        [StringLength(10)]
        public string UnitNumber { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [ForeignKey(nameof(ApartmentId))]
        public ApartmentModel Apartment { get; set; } = null!;
    }
}

