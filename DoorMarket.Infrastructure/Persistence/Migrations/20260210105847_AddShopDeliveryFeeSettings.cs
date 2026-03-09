using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopDeliveryFeeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryBaseFeeUsd",
                table: "Shops",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 2m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryPerKmUsd",
                table: "Shops",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0.55m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryBaseFeeUsd",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "DeliveryPerKmUsd",
                table: "Shops");
        }
    }
}
