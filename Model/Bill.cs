using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class Bill
    {
        public int Id { get; set; }

        // Required Foreign Key to link bill to a specific user
        [Required]
        public int ApartmentId { get; set; }

        [Required]
        public int BillingPeriodId { get; set; }

        [ForeignKey("BillingPeriodId")]
        public Apartment Apartment { get; set; } = null!;

        // The user (tenant) responsible for this bill

        [Required]
        public int TenantId { get; set; }

        [ForeignKey("TenantId")]
        public UserList Tenant { get; set; } = null!;

        // financial details

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Amount Due")]
        public decimal AmountDue { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Amount Paid")]
        public decimal AmountPaid { get; set; } = 0.00m; // default to unpaid

        public bool IsPaid => AmountDue <= AmountPaid;

        public DateTime DueDate { get; set; }

        public DateTime? PaymentDate { get; set; }

        public DateTime GeneratedDate { get; set; }


    }
}
