using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Apartment.Enums;
using Apartment.Model;

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

        // Foreign Key to link the bill to a specific lease
        [Required]
        public int LeaseId { get; set; }

        [ForeignKey("LeaseId")]
        public Lease Lease { get; set; } = null!;


        // 
        [ForeignKey("ApartmentId")]
        public ApartmentModel Apartment { get; set; } = null!;

        [ForeignKey("BillingPeriodId")]
        public BillingPeriod BillingPeriod { get; set; } = null!;

        [ForeignKey("TenantUserId")]
        public User? TenantUser { get; set; }



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

        public DateTime? DateFullySettled { get; set; }

        public DateTime GeneratedDate { get; set; }

        public Enums.BillStatus Status { get; set; } = Enums.BillStatus.Unpaid;

        [Required]
        public BillType Type { get; set; } = BillType.Rent;

        [StringLength(200)]
        public string? Description { get; set; }

        // Foreign key to link a late fee bill to its original rent bill
        public int? ParentBillId { get; set; }

        [ForeignKey("ParentBillId")]
        public Bill? ParentBill { get; set; }


        public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
    }
}
