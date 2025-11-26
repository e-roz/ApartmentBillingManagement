using System.ComponentModel.DataAnnotations;

namespace Apartment.ViewModels
{
    /// <summary>
    /// Input model for tenant operations to prevent overposting attacks
    /// Only includes fields that should be editable by users
    /// </summary>
    public class TenantInputModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string PrimaryEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PrimaryPhone { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime LeaseStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime LeaseEndDate { get; set; }

        public int? ApartmentId { get; set; }
    }
}

