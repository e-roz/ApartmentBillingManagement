using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseIdToBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeaseId",
                table: "Bills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_LeaseId",
                table: "Bills",
                column: "LeaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Leases_LeaseId",
                table: "Bills",
                column: "LeaseId",
                principalTable: "Leases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Leases_LeaseId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_LeaseId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                table: "Bills");
        }
    }
}
