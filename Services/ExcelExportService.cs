using Apartment.ViewModels;
using ClosedXML.Excel;
using Apartment.Model;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Apartment.Services
{
    public class ExcelExportService
    {
        public XLWorkbook BuildAuditLogWorkbook(IEnumerable<AuditLog> auditLogs)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Audit Logs");
            var currentRow = 1;

            worksheet.Cell(currentRow, 1).Value = "Timestamp";
            worksheet.Cell(currentRow, 2).Value = "User";
            worksheet.Cell(currentRow, 3).Value = "Action";
            worksheet.Cell(currentRow, 4).Value = "Details";
            worksheet.Cell(currentRow, 5).Value = "IP Address";
            worksheet.Cell(currentRow, 6).Value = "Success";
            worksheet.Range(currentRow, 1, currentRow, 6).Style.Font.Bold = true;

            foreach (var log in auditLogs)
            {
                currentRow++;
                worksheet.Cell(currentRow, 1).Value = log.Timestamp;
                worksheet.Cell(currentRow, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                worksheet.Cell(currentRow, 2).Value = log.User?.Username ?? "System";
                worksheet.Cell(currentRow, 3).Value = log.Action.ToString();
                worksheet.Cell(currentRow, 4).Value = log.Details;
                worksheet.Cell(currentRow, 5).Value = log.IpAddress;
                worksheet.Cell(currentRow, 6).Value = log.Success;
            }

            worksheet.Columns().AdjustToContents();

            return workbook;
        }
        public XLWorkbook BuildBillingSummaryWorkbook(BillingSummaryViewModel summary)
        {
            var workbook = new XLWorkbook();
            var summarySheet = workbook.Worksheets.Add("Summary");

            summarySheet.Cell(1, 1).Value = "Billing Summary";
            summarySheet.Cell(1, 1).Style.Font.Bold = true;
            summarySheet.Cell(2, 1).Value = $"Period:";
            summarySheet.Cell(2, 2).Value = summary.PeriodLabel;
            summarySheet.Cell(3, 1).Value = $"Generated:";
            summarySheet.Cell(3, 2).Value = DateTime.Now;
            summarySheet.Cell(3, 2).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

            summarySheet.Cell(5, 1).Value = "Metric";
            summarySheet.Cell(5, 2).Value = "Value";
            summarySheet.Range(5, 1, 5, 2).Style.Font.Bold = true;

            var metrics = new (string Label, object Value)[]
            {
                ("Total Billed", summary.TotalBilled),
                ("Total Collected", summary.TotalCollected),
                ("Total Outstanding", summary.TotalOutstanding),
                ("Collection Efficiency (%)", summary.CollectionEfficiency),
                ("Month-over-Month Change (%)", summary.MonthOverMonthChange),
                ("Occupied Units", summary.OccupiedUnits),
                ("Vacant Units", summary.VacantUnits)
            };

            var row = 6;
            foreach (var metric in metrics)
            {
                summarySheet.Cell(row, 1).Value = metric.Label;
                var cell = summarySheet.Cell(row, 2);
                switch (metric.Value)
                {
                    case decimal dec:
                        cell.Value = dec;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                        break;
                    case double dbl:
                        cell.Value = dbl;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    default:
                        cell.Value = metric.Value?.ToString() ?? string.Empty;
                        break;
                }

                row++;
            }

            summarySheet.Columns(1, 2).AdjustToContents();

            var detailSheet = workbook.Worksheets.Add("Bill Details");
            var headers = new[]
            {
                "Period",
                "Tenant",
                "Unit",
                "Amount Due",
                "Amount Paid",
                "Outstanding",
                "Due Date",
                "Date Fully Settled",
                "Status"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                detailSheet.Cell(1, i + 1).Value = headers[i];
                detailSheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            if (summary.BillDetails.Any())
            {
                var detailRow = 2;
                foreach (var detail in summary.BillDetails)
                {
                    detailSheet.Cell(detailRow, 1).Value = detail.PeriodLabel;
                    detailSheet.Cell(detailRow, 2).Value = detail.TenantName;
                    detailSheet.Cell(detailRow, 3).Value = detail.UnitNumber;
                    detailSheet.Cell(detailRow, 4).Value = detail.AmountDue;
                    detailSheet.Cell(detailRow, 5).Value = detail.AmountPaid;
                    detailSheet.Cell(detailRow, 6).Value = detail.Outstanding;
                    detailSheet.Cell(detailRow, 7).Value = detail.DueDate;
                    detailSheet.Cell(detailRow, 8).Value = detail.DateFullySettled;
                    detailSheet.Cell(detailRow, 9).Value = detail.Status;

                    detailSheet.Cell(detailRow, 4).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(detailRow, 5).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(detailRow, 6).Style.NumberFormat.Format = "#,##0.00";
                    detailSheet.Cell(detailRow, 7).Style.DateFormat.Format = "yyyy-MM-dd";
                    detailSheet.Cell(detailRow, 8).Style.DateFormat.Format = "yyyy-MM-dd";

                    detailRow++;
                }

                var tableRange = detailSheet.Range(1, 1, summary.BillDetails.Count + 1, headers.Length);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }
            else
            {
                detailSheet.Cell(2, 1).Value = "No bill data for this selection.";
            }

            detailSheet.Columns().AdjustToContents();
            return workbook;
        }
    }
}


