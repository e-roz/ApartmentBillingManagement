using System.ComponentModel.DataAnnotations;

namespace Apartment.ViewModels
{
    public class ApartmentList
    {
        public int Id { get; set; }

        [Display(Name = "Unit number")]
        public string UnitNumber { get; set; } = string.Empty;

        [Display(Name = "Monthly Rent")]
        [DataType(DataType.Currency)]
        public decimal MonthlyRent { get; set; }

        [Display(Name = "Status")]
        public string StatusDisplay { get; set; } = "Vacant";

        [Display(Name = "Current Tenant")]
        public string? TenantName { get; set; } = "N/A";
        public int? TenantId { get; set; }
    }
}

