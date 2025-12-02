using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class RenamePaymentDateToDateFullySettled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PaymentDate",
                table: "Invoices",
                newName: "DateFullySettled");

            migrationBuilder.RenameColumn(
                name: "PaymentDate",
                table: "Bills",
                newName: "DateFullySettled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DateFullySettled",
                table: "Invoices",
                newName: "PaymentDate");

            migrationBuilder.RenameColumn(
                name: "DateFullySettled",
                table: "Bills",
                newName: "PaymentDate");
        }
    }
}
