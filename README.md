# Apartment
This project is for our case Study in event driven programming and advance database system. Our main tools here is visual studio(c#) and sql (smss)

## Getting Started

This guide will walk you through setting up the project locally.

### Prerequisites

Make sure you have the following software installed:
- **.NET 8 SDK:** [Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server:** An instance of SQL Server (e.g., Express, Developer, or LocalDB).
- **Node.js:** (Optional, if you need to manage client-side packages) [Download Node.js](https://nodejs.org/)
- **.NET EF Core Tools:** Install the global tool by running the following command:
  ```sh
  dotnet tool install --global dotnet-ef
  ```

### 1. Clone the Repository

Clone this repository to your local machine:
```sh
git clone <your-repository-url>
cd ApartmentBillingManagement
```

### 2. Configure Local Settings

You need to provide a database connection string for your local development environment.

1.  Create a new file named `appsettings.Development.json` in the main project directory (`ApartmentBillingManagement/`).
2.  Add the following JSON configuration, replacing the `ConnectionStrings` value with your own local SQL Server connection string.

    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "ConnectionStrings": {
        "DefaultConnection": "Server=your_server_name;Database=ApartmentDB_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False"
      }
    }
    ```
    *Replace `your_server_name` with your local SQL server instance name (e.g., `(localdb)\\mssqllocaldb`, `.\\SQLEXPRESS`, or `localhost`).*

### 3. Install Dependencies

Restore the .NET and client-side packages.

- **.NET Dependencies:**
  ```sh
  dotnet restore
  ```
- **NPM Dependencies (if applicable):**
  If you see a `package.json` file, run:
  ```sh
  npm install
  ```

### 4. Apply Database Migrations

This project uses EF Core Migrations to set up the database schema. Choose the method that matches your development environment.

<details>
<summary><strong>Option 1: Using .NET CLI (Recommended)</strong></summary>

Make sure you are in the `ApartmentBillingManagement` directory, then run the following command. It will create the database (if it doesn't exist) and apply all migrations.

```sh
dotnet ef database update
```
</details>

<details>
<summary><strong>Option 2: Using Visual Studio PMC</strong></summary>

1.  Open the solution in Visual Studio.
2.  Open the Package Manager Console (`View` > `Other Windows` > `Package Manager Console`).
3.  Ensure the "Default project" dropdown is set to `Apartment`.
4.  Run the following command:

```powershell
Update-Database
```
</details>

### 5. Run the Application

You can now run the application. From the `ApartmentBillingManagement` directory, run:

```sh
dotnet watch run
```
This command will build and run the application, automatically relaunching it if you make any code changes. The application should be available at a local address shown in the console (e.g., `https://localhost:7043`).

### Troubleshooting Migrations
If you get an error during the `database update` command, it's often because a local database already exists in an inconsistent state. The easiest fix is to start fresh.

1.  **Open SQL Server Management Studio (SSMS) or your database tool.**
2.  **Find the database** you named in your `appsettings.Development.json` (e.g., `ApartmentDB_Dev`).
3.  **Delete the database.**
4.  **Run the update command again.** EF Core will now create a clean database and apply all migrations from the beginning.

---

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