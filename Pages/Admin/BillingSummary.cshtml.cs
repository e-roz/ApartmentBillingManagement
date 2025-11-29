using Apartment.Data;
using Apartment.Services;
using Apartment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class BillingSummaryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AdminReportingService _reportingService;
        private readonly ExcelExportService _excelExportService;

        public BillingSummaryModel(ApplicationDbContext context, AdminReportingService reportingService, ExcelExportService excelExportService)
        {
            _context = context;
            _reportingService = reportingService;
            _excelExportService = excelExportService;
        }

        [BindProperty(SupportsGet = true)]
        public string? SelectedPeriodKey { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedApartmentId { get; set; }

        public BillingSummaryViewModel Summary { get; set; } = new();

        public List<SelectListItem> PeriodOptions { get; set; } = new();
        public List<SelectListItem> ApartmentOptions { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadFilterOptionsAsync();
            Summary = await _reportingService.GetSummaryAsync(SelectedPeriodKey, SelectedApartmentId);
        }

        public async Task<IActionResult> OnGetExportAsync()
        {
            var summary = await _reportingService.GetSummaryAsync(SelectedPeriodKey, SelectedApartmentId, includeDetails: true);
            if (!summary.BillDetails.Any() && summary.TotalBilled == 0)
            {
                TempData["ErrorMessage"] = "No data available to export for the selected filters.";
                return RedirectToPage(new { SelectedPeriodKey, SelectedApartmentId });
            }

            var workbook = _excelExportService.BuildBillingSummaryWorkbook(summary);
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"BillingSummary_{summary.PeriodLabel.Replace(' ', '_')}_{DateTime.UtcNow:yyyyMMddHHmm}.xlsx";
            stream.Position = 0;
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private async Task LoadFilterOptionsAsync()
        {
            var periods = await _context.BillingPeriods
                .AsNoTracking()
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.PeriodKey)
                .Select(p => new SelectListItem
                {
                    Value = p.PeriodKey,
                    Text = $"{p.MonthName} {p.Year}",
                    Selected = p.PeriodKey == SelectedPeriodKey
                })
                .ToListAsync();

            PeriodOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = string.Empty, Text = "All Periods", Selected = string.IsNullOrEmpty(SelectedPeriodKey) }
            };
            PeriodOptions.AddRange(periods);

            var apartments = await _context.Apartments
                .AsNoTracking()
                .OrderBy(a => a.UnitNumber)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"Unit {a.UnitNumber}",
                    Selected = a.Id == SelectedApartmentId
                })
                .ToListAsync();

            ApartmentOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = string.Empty, Text = "All Apartments", Selected = !SelectedApartmentId.HasValue }
            };
            ApartmentOptions.AddRange(apartments);
        }
    }
}

