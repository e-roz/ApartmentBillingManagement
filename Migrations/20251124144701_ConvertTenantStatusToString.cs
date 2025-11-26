using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class ConvertTenantStatusToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tenants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(
                """
                UPDATE Tenants
                SET Status = CASE Status
                    WHEN '0' THEN 'Prospective'
                    WHEN '1' THEN 'Active'
                    WHEN '2' THEN 'Inactive'
                    WHEN '3' THEN 'Evicted'
                    ELSE 'Prospective'
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaymentDate",
                table: "Invoices",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_DueDate",
                table: "Bills",
                column: "DueDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Status",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PaymentDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Bills_DueDate",
                table: "Bills");

            migrationBuilder.Sql(
                """
                UPDATE Tenants
                SET Status = CASE Status
                    WHEN 'Prospective' THEN '0'
                    WHEN 'Active' THEN '1'
                    WHEN 'Inactive' THEN '2'
                    WHEN 'Evicted' THEN '3'
                    ELSE '0'
                END
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
