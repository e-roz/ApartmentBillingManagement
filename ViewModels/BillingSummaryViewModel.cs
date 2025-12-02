using System.Collections.Generic;

namespace Apartment.ViewModels
{
    public class BillingSummaryViewModel
    {
        public string PeriodLabel { get; set; } = "All Periods";
        public string? PeriodKey { get; set; }
        public int? ApartmentId { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal CollectionEfficiency { get; set; }
        public decimal MonthOverMonthChange { get; set; }
        public int OccupiedUnits { get; set; }
        public int VacantUnits { get; set; }
        public List<TrendPoint> RevenueTrend { get; set; } = new();
        public List<AgingBucket> AgingBuckets { get; set; } = new();
        public List<OverdueTenant> TopOverdueTenants { get; set; } = new();
        public List<BillDetail> BillDetails { get; set; } = new();
    }

    public class TrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Billed { get; set; }
        public decimal Collected { get; set; }
    }

    public class AgingBucket
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class OverdueTenant
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public decimal OutstandingAmount { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class BillDetail
    {
        public int BillId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Outstanding { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? DateFullySettled { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}


