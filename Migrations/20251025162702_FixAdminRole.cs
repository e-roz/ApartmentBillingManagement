using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class FixAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix admin role - set the first user (usually admin) to role 1
            migrationBuilder.Sql(@"
                UPDATE Users 
                SET Role = 1 
                WHERE Id = (SELECT TOP 1 Id FROM Users ORDER BY Id ASC)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
