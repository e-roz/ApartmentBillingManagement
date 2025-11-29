using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class AddBillIdToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillId",
                table: "Invoices",
                column: "BillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Bills_BillId",
                table: "Invoices",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Bills_BillId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BillId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillId",
                table: "Invoices");
        }
    }
}
