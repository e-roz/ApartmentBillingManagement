using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class DropUserApartmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_Users_TenantId",
                table: "Apartments");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Apartments_ApartmentId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ApartmentId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ApartmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LeaseEnd",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LeaseStart",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MonthlyRent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Apartments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApartmentId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseEnd",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseStart",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRent",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNumber",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Apartments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApartmentId",
                table: "Users",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_Users_TenantId",
                table: "Apartments",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Apartments_ApartmentId",
                table: "Users",
                column: "ApartmentId",
                principalTable: "Apartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
