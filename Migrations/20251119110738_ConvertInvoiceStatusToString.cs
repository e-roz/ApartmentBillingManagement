using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class ConvertInvoiceStatusToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(
                """
                UPDATE Invoices
                SET Status = CASE Status
                    WHEN '0' THEN 'Pending'
                    WHEN '1' THEN 'Paid'
                    WHEN '2' THEN 'Overdue'
                    WHEN '3' THEN 'Cancelled'
                    WHEN '4' THEN 'Partial'
                    ELSE Status
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Invoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql(
                """
                UPDATE Invoices
                SET Status = CASE Status
                    WHEN 'Pending' THEN '0'
                    WHEN 'Paid' THEN '1'
                    WHEN 'Overdue' THEN '2'
                    WHEN 'Cancelled' THEN '3'
                    WHEN 'Partial' THEN '4'
                    ELSE '0'
                END
                """);
        }
    }
}
