using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryNameEn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Categories");
        }
    }
}
