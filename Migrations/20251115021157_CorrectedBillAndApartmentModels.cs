using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class CorrectedBillAndApartmentModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, set all TenantId values to NULL to avoid FK conflicts
            migrationBuilder.Sql(@"
                UPDATE Apartments SET TenantId = NULL WHERE TenantId IS NOT NULL;
            ");

            // Drop the foreign key constraint from Apartments to Users
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_Users_TenantId",
                table: "Apartments");

            // Drop the index on TenantId
            migrationBuilder.DropIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments");

            // Drop the TenantId column from Apartments table
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Apartments");

            // Fix Bill.TenantId to reference Tenant instead of User
            // First, check if TenantId1 exists (shadow property) and migrate data if needed
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'TenantId1')
                BEGIN
                    UPDATE Bills SET TenantId = TenantId1 WHERE TenantId1 IS NOT NULL;
                    ALTER TABLE Bills DROP CONSTRAINT IF EXISTS FK_Bills_Tenants_TenantId1;
                    DROP INDEX IF EXISTS IX_Bills_TenantId1 ON Bills;
                    ALTER TABLE Bills DROP COLUMN TenantId1;
                END
            ");

            // Drop existing FK if it references Users
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Bills_Users_TenantId')
                BEGIN
                    ALTER TABLE Bills DROP CONSTRAINT FK_Bills_Users_TenantId;
                END
            ");

            // Add FK from Bills to Tenants
            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                table: "Bills",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK from Bills to Tenants
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                table: "Bills");

            // Re-add TenantId column to Apartments
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Apartments",
                type: "int",
                nullable: true);

            // Re-create index
            migrationBuilder.CreateIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments",
                column: "TenantId");

            // Re-add FK from Apartments to Users
            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_Users_TenantId",
                table: "Apartments",
                column: "TenantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
