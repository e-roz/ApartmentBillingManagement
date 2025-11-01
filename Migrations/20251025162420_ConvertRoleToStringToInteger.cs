using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRoleToStringToInteger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, add a temporary column to store integer values
            migrationBuilder.AddColumn<int>(
                name: "RoleTemp",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 3);

            // Convert string values to integers
            migrationBuilder.Sql(@"
                UPDATE Users 
                SET RoleTemp = CASE 
                    WHEN Role = 'Admin' THEN 1
                    WHEN Role = 'Manager' THEN 2
                    WHEN Role = 'User' THEN 3
                    ELSE 3
                END");

            // Drop the old Role column
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            // Rename the temporary column to Role
            migrationBuilder.RenameColumn(
                name: "RoleTemp",
                table: "Users",
                newName: "Role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add a temporary string column
            migrationBuilder.AddColumn<string>(
                name: "RoleTemp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "User");

            // Convert integer values back to strings
            migrationBuilder.Sql(@"
                UPDATE Users 
                SET RoleTemp = CASE 
                    WHEN Role = 1 THEN 'Admin'
                    WHEN Role = 2 THEN 'Manager'
                    WHEN Role = 3 THEN 'User'
                    ELSE 'User'
                END");

            // Drop the integer Role column
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            // Rename the temporary column back to Role
            migrationBuilder.RenameColumn(
                name: "RoleTemp",
                table: "Users",
                newName: "Role");
        }
    }
}
