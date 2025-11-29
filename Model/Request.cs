using Apartment.Enums;
using Apartment.Model;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apartment.Model
{
    public class Request
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public RequestType RequestType { get; set; }

        [Required]
        public RequestStatus Status { get; set; }

        [Required]
        public RequestPriority Priority { get; set; } = RequestPriority.Medium;

        [Required]
        public DateTime DateSubmitted { get; set; }

        public int? SubmittedByUserId { get; set; }

        [ForeignKey("SubmittedByUserId")]
        public virtual User? SubmittedByUser { get; set; }

        public int? ApartmentId { get; set; }

        [ForeignKey("ApartmentId")]
        public virtual ApartmentModel? Apartment { get; set; }
    }
}
