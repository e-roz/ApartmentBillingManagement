using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentIdToTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApartmentId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId1",
                table: "Bills",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ApartmentId",
                table: "Tenants",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId1",
                table: "Bills",
                column: "TenantId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Tenants_TenantId1",
                table: "Bills",
                column: "TenantId1",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Apartments_ApartmentId",
                table: "Tenants",
                column: "ApartmentId",
                principalTable: "Apartments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Tenants_TenantId1",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Apartments_ApartmentId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ApartmentId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId1",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ApartmentId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TenantId1",
                table: "Bills");
        }
    }
}
