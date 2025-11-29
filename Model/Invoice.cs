using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Apartment.Enums;

namespace Apartment.Model
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int TenantUserId { get; set; }

        public int ApartmentId { get; set; }

        // Link to the Bill that generated this invoice
        public int? BillId { get; set; }


        // The name of the bill

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]

        public decimal AmountDue { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime IssueDate { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Online Payment";

        [Column(TypeName = "nvarchar(20)")]
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;


        //payment tracking
        public DateTime? PaymentDate { get; set; }

        [StringLength(500)]
        public string? ReceiptImagePath { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [ForeignKey("TenantUserId")]
        public User? TenantUser { get; set; }

        [ForeignKey("ApartmentId")]
        public ApartmentModel? Apartment { get; set; }

        [ForeignKey("BillId")]
        public Bill? Bill { get; set; }
    }
}
