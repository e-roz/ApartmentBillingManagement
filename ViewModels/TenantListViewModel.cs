namespace Apartment.ViewModels
{
    public class TenantListViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PrimaryEmail { get; set; } = string.Empty;
        public string PrimaryPhone { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public string LeaseStatus { get; set; } = string.Empty;
        public string AssignedUnit { get; set; } = string.Empty;
    }
}

