using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class PaymentAllocation
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to the Invoice (representing the payment received)
        [Required]
        public int InvoiceId { get; set; }

        // Foreign Key to the Bill (representing the debt owed)
        [Required]
        public int BillId { get; set; }

        // The precise amount of the total payment that was used to pay off this specific bill
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountApplied { get; set; }

        // Navigation properties
        [ForeignKey(nameof(InvoiceId))]
        public Invoice Invoice { get; set; } = null!;

        [ForeignKey(nameof(BillId))]
        public Bill Bill { get; set; } = null!;
    }
}
