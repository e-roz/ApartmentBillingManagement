# Apartment
This project is for our case Study in event driven programming and advance database system. Our main tools here is visual studio(c#) and sql (smss)

## Manager Billing Summary
- `Pages/Manager/BillingSummary` now provides portfolio-level KPIs (billed, collected, outstanding, collection efficiency), trend charts, and a list of top overdue tenants.
- Filters support any billing period and individual apartment units to narrow the analytics. An `Export Excel` action produces a workbook with the KPI snapshot plus a detailed bill ledger via ClosedXML.
- Chart.js is loaded via CDN to render the revenue trend visuals.

## LogSnag Alerts
The system can emit LogSnag events for key workflows (bill generation, payment recording, KPI threshold breaches).

1. Configure credentials in `appsettings.Development.json` / user secrets:
   ```json
   "LogSnag": {
     "Project": "your-project",
     "Token": "secret-token",
     "DefaultChannel": "billing",
     "OutstandingWarningThreshold": 50000,
     "CollectionEfficiencyWarning": 85
   }
   ```
2. Production secrets should be injected through environment variables or the host’s secret store.
3. Events are published for:
   - High outstanding balance or low collection efficiency detected on the Billing Summary.
   - Successful bill generation batches.
   - Payment recordings from the manager portal.

If LogSnag credentials are not supplied, the client safely no-ops.