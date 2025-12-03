using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apartment.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentTypeToApartmentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApartmentType",
                table: "Apartments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApartmentType",
                table: "Apartments");
        }
    }
}
