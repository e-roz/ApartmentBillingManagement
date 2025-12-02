using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeAndParentIdToBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptImagePath",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Bills",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentBillId",
                table: "Bills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Bills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ParentBillId",
                table: "Bills",
                column: "ParentBillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Bills_ParentBillId",
                table: "Bills",
                column: "ParentBillId",
                principalTable: "Bills",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Bills_ParentBillId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_ParentBillId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ParentBillId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Bills");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptImagePath",
                table: "Invoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
