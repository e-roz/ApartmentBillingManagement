using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class Bill
    {
        public int Id { get; set; }

        // Required Foreign Key to link bill to a specific user



        // Links this bill to a specific apartment unit
        [Required]
        public int ApartmentId { get; set; }


        // Links this bill to the overall billing month
        [Required]
        public int BillingPeriodId { get; set; }


        //  Link this bill to the user/tenant responsible for payment
        [Required]
        public int TenantUserId { get; set; }

        // Obsolete: Kept for migration compatibility - remove after final cleanup
        [Obsolete("Use TenantUserId instead - kept for migration")]
        public int TenantId { get; set; }

        // 
        [ForeignKey("ApartmentId")]
        public ApartmentModel Apartment { get; set; } = null!;

        [ForeignKey("BillingPeriodId")]
        public BillingPeriod BillingPeriod { get; set; } = null!;

        [ForeignKey("TenantUserId")]
        public User TenantUser { get; set; } = null!;

        // Obsolete: Kept for migration compatibility
        [Obsolete("Use TenantUser instead - kept for migration")]
        [ForeignKey("TenantId")]
        public Tenant? Tenant { get; set; }



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
