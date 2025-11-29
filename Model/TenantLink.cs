using System.ComponentModel.DataAnnotations;

namespace Apartment.Model
{
    public class TenantLink
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string ApartmentId { get; set; } = string.Empty;

        public DateTime LinkedDate { get; set; } = DateTime.UtcNow;
    }
}

