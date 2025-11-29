using Apartment.Data;
using Apartment.Options;
using Apartment.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Apartment.Services
{
    public class AdminReportingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogSnagClient _logSnagClient;
        private readonly LogSnagOptions _logSnagOptions;

        public AdminReportingService(
            ApplicationDbContext context,
            ILogSnagClient logSnagClient,
            IOptions<LogSnagOptions> logSnagOptions)
        {
            _context = context;
            _logSnagClient = logSnagClient;
            _logSnagOptions = logSnagOptions.Value;
        }

        public async Task<BillingSummaryViewModel> GetSummaryAsync(string? periodKey = null, int? apartmentId = null, bool includeDetails = false, CancellationToken cancellationToken = default)
        {
            var billsQuery = _context.Bills
                .AsNoTracking()
                .Include(b => b.BillingPeriod)
                .Include(b => b.Tenant)
                .Include(b => b.Apartment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(periodKey))
            {
                billsQuery = billsQuery.Where(b => b.BillingPeriod.PeriodKey == periodKey);
            }

            if (apartmentId.HasValue)
            {
                billsQuery = billsQuery.Where(b => b.ApartmentId == apartmentId.Value);
            }

            var bills = await billsQuery
                .Select(b => new
                {
                    b.AmountDue,
                    b.AmountPaid,
                    b.DueDate,
                    b.PaymentDate,
                    PeriodKey = b.BillingPeriod.PeriodKey,
                    b.BillingPeriod.MonthName,
                    b.BillingPeriod.Year,
                    b.Id,
                    TenantName = b.Tenant.FullName ?? $"Tenant #{b.TenantId}",
                    b.TenantId,
                    UnitNumber = b.Apartment.UnitNumber ?? $"Unit #{b.ApartmentId}"
                })
                .ToListAsync(cancellationToken);

            var totalBilled = bills.Sum(b => b.AmountDue);
            var totalCollected = bills.Sum(b => b.AmountPaid);
            var totalOutstanding = totalBilled - totalCollected;
            var collectionEfficiency = totalBilled == 0 ? 0 : Math.Round(totalCollected / totalBilled * 100, 2);

            var trend = bills
                .GroupBy(b => new { b.PeriodKey, b.MonthName, b.Year })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.PeriodKey)
                .Select(g => new TrendPoint
                {
                    Label = $"{g.Key.MonthName} {g.Key.Year}",
                    Billed = g.Sum(x => x.AmountDue),
                    Collected = g.Sum(x => x.AmountPaid)
                })
                .ToList();

            var periodLabel = "All Periods";
            if (!string.IsNullOrWhiteSpace(periodKey))
            {
                var selected = trend.FirstOrDefault(t => bills.Any(b => b.PeriodKey == periodKey && $"{b.MonthName} {b.Year}" == t.Label));
                periodLabel = selected?.Label ?? periodKey;
            }

            var agingBuckets = new List<AgingBucket>
            {
                new() { Label = "Current (0-30)", Amount = 0m },
                new() { Label = "31-60 Days", Amount = 0m },
                new() { Label = "61-90 Days", Amount = 0m },
                new() { Label = "90+ Days", Amount = 0m }
            };

            var today = DateTime.UtcNow.Date;
            foreach (var bill in bills)
            {
                var outstanding = bill.AmountDue - bill.AmountPaid;
                if (outstanding <= 0)
                {
                    continue;
                }

                var daysPastDue = (today - bill.DueDate.Date).Days;
                var bucketIndex = daysPastDue switch
                {
                    <= 30 => 0,
                    <= 60 => 1,
                    <= 90 => 2,
                    _ => 3
                };

                agingBuckets[bucketIndex].Amount += outstanding;
            }

            var overdueTenants = bills
                .Where(b => b.AmountPaid < b.AmountDue && b.DueDate < today)
                .GroupBy(b => new { b.TenantId, b.TenantName, b.UnitNumber })
                .Select(g => new OverdueTenant
                {
                    TenantId = g.Key.TenantId,
                    TenantName = g.Key.TenantName,
                    UnitNumber = g.Key.UnitNumber,
                    OutstandingAmount = g.Sum(x => x.AmountDue - x.AmountPaid),
                    DaysOverdue = (int)Math.Round(g.Average(x => (today - x.DueDate.Date).TotalDays))
                })
                .OrderByDescending(t => t.OutstandingAmount)
                .Take(5)
                .ToList();

            var occupiedUnits = await _context.Apartments.CountAsync(a => a.IsOccupied, cancellationToken);
            var totalUnits = await _context.Apartments.CountAsync(cancellationToken);
            var vacantUnits = totalUnits - occupiedUnits;

            var monthOverMonthChange = 0m;
            if (trend.Count >= 2)
            {
                var current = trend.Last().Collected;
                var previous = trend[^2].Collected;
                if (previous != 0)
                {
                    monthOverMonthChange = Math.Round((current - previous) / previous * 100, 2);
                }
            }

            var summary = new BillingSummaryViewModel
            {
                PeriodLabel = periodLabel,
                PeriodKey = periodKey,
                ApartmentId = apartmentId,
                TotalBilled = totalBilled,
                TotalCollected = totalCollected,
                TotalOutstanding = totalOutstanding,
                CollectionEfficiency = collectionEfficiency,
                MonthOverMonthChange = monthOverMonthChange,
                OccupiedUnits = occupiedUnits,
                VacantUnits = vacantUnits,
                RevenueTrend = trend,
                AgingBuckets = agingBuckets,
                TopOverdueTenants = overdueTenants
            };

            if (includeDetails)
            {
                summary.BillDetails = bills
                    .OrderByDescending(b => b.Year)
                    .ThenByDescending(b => b.PeriodKey)
                    .Select(b => new BillDetail
                    {
                        BillId = b.Id,
                        PeriodLabel = $"{b.MonthName} {b.Year}",
                        TenantName = b.TenantName,
                        UnitNumber = b.UnitNumber,
                        AmountDue = b.AmountDue,
                        AmountPaid = b.AmountPaid,
                        Outstanding = b.AmountDue - b.AmountPaid,
                        DueDate = b.DueDate,
                        PaymentDate = b.PaymentDate,
                        Status = DetermineStatus(b.AmountDue, b.AmountPaid, b.DueDate, b.PaymentDate)
                    })
                    .ToList();
            }

            await EmitAlertsAsync(summary, cancellationToken);
            return summary;
        }

        private static string DetermineStatus(decimal amountDue, decimal amountPaid, DateTime dueDate, DateTime? paymentDate)
        {
            if (amountPaid >= amountDue)
            {
                return "Paid";
            }

            if (amountPaid > 0)
            {
                return dueDate < DateTime.UtcNow.Date ? "Overdue (Partial)" : "Partial";
            }

            return dueDate < DateTime.UtcNow.Date ? "Overdue" : "Unpaid";
        }

        private async Task EmitAlertsAsync(BillingSummaryViewModel summary, CancellationToken cancellationToken)
        {
            if (!_logSnagOptions.IsConfigured)
            {
                return;
            }

            var tasks = new List<Task>();

            if (_logSnagOptions.OutstandingWarningThreshold > 0 &&
                summary.TotalOutstanding >= _logSnagOptions.OutstandingWarningThreshold)
            {
                tasks.Add(SendSafeAsync(new LogSnagEvent
                {
                    Event = "High Outstanding Balance",
                    Description = $"Outstanding reached {summary.TotalOutstanding:C} for {summary.PeriodLabel}",
                    Icon = "⚠️",
                    Notify = true,
                    Tags = new Dictionary<string, string>
                    {
                        { "period", summary.PeriodLabel },
                        { "outstanding", summary.TotalOutstanding.ToString("0.##") }
                    }
                }, cancellationToken));
            }

            if (_logSnagOptions.CollectionEfficiencyWarning > 0 &&
                summary.CollectionEfficiency <= _logSnagOptions.CollectionEfficiencyWarning)
            {
                tasks.Add(SendSafeAsync(new LogSnagEvent
                {
                    Event = "Low Collection Efficiency",
                    Description = $"Collection efficiency at {summary.CollectionEfficiency:0.##}% for {summary.PeriodLabel}",
                    Icon = "📉",
                    Notify = false,
                    Tags = new Dictionary<string, string>
                    {
                        { "period", summary.PeriodLabel },
                        { "efficiency", summary.CollectionEfficiency.ToString("0.##") }
                    }
                }, cancellationToken));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        private async Task SendSafeAsync(LogSnagEvent logEvent, CancellationToken cancellationToken)
        {
            try
            {
                await _logSnagClient.PublishAsync(logEvent, cancellationToken);
            }
            catch
            {
                // logging integration failures should not block reporting flow
            }
        }
    }
}

