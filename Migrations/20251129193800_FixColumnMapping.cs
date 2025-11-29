using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class FixColumnMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_Users_CurrentTenantId",
                table: "Apartments");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Tenants_TenantId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_ReceiverUserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantID",
                table: "Users");

            migrationBuilder.DropTable(
                name: "TenantLinks");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_LeaseStatus",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_CurrentTenantId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "TenantID",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CurrentTenantId",
                table: "Apartments");

            migrationBuilder.RenameColumn(
                name: "LeaseStatus",
                table: "Users",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Bills",
                newName: "TenantUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Bills_TenantId",
                table: "Bills",
                newName: "IX_Bills_TenantUserId");

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
                name: "FK_Bills_Users_TenantUserId",
                table: "Bills",
                column: "TenantUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_ReceiverUserId",
                table: "Messages",
                column: "ReceiverUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_Users_TenantId",
                table: "Apartments");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Users_TenantUserId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_ReceiverUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MonthlyRent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Users",
                newName: "LeaseStatus");

            migrationBuilder.RenameColumn(
                name: "TenantUserId",
                table: "Bills",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Bills_TenantUserId",
                table: "Bills",
                newName: "IX_Bills_TenantId");

            migrationBuilder.AddColumn<int>(
                name: "TenantID",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentTenantId",
                table: "Apartments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApartmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LeaseEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrimaryEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UnitNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LeaseStatus",
                table: "Users",
                column: "LeaseStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantID",
                table: "Users",
                column: "TenantID",
                unique: true,
                filter: "[TenantID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId",
                table: "Invoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_CurrentTenantId",
                table: "Apartments",
                column: "CurrentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ApartmentId",
                table: "Tenants",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_Users_CurrentTenantId",
                table: "Apartments",
                column: "CurrentTenantId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                table: "Bills",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Tenants_TenantId",
                table: "Invoices",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_ReceiverUserId",
                table: "Messages",
                column: "ReceiverUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantID",
                table: "Users",
                column: "TenantID",
                principalTable: "Tenants",
                principalColumn: "Id");
        }
    }
}
