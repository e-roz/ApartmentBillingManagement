using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public int SenderUserId { get; set; }

        [ForeignKey("SenderUserId")]
        public virtual User? Sender { get; set; }

        [Required]
        public int ReceiverUserId { get; set; }

        [ForeignKey("ReceiverUserId")]
        public virtual User? Receiver { get; set; }

        public int? AssociatedRequestId { get; set; }

        [ForeignKey("AssociatedRequestId")]
        public virtual Request? AssociatedRequest { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
    }
}
