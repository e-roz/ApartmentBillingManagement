using System.ComponentModel.DataAnnotations;

namespace Apartment.Model
{
    public class BillingPeriod
    {
        public int Id { get; set; }


        // format: yyyy-mm (e.g., 2023-09)
        [Required]
        [StringLength(7)]
        public string PeriodKey { get; set; }


        [Required]
        [StringLength(50)]
        public string MonthName { get; set; }

        [Required]
        public int Year { get; set; }

        public ICollection<Bill> Bills { get; set; } = new List<Bill>();


    }
}
