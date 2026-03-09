using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutWorkflowAndPercentCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopPayouts_ShopId_PeriodStartUtc_PeriodEndUtc",
                table: "ShopPayouts");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "ShopPayouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "ShopPayouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ShopPayouts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ShopPayouts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByUserId",
                table: "ShopPayouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "ShopPayouts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "ShopPayouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "ShopPayouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ShopPayouts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "PlatformFeeMode",
                table: "Products",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Flat");

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeePercent",
                table: "Products",
                type: "decimal(9,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShopPayoutStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopPayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopPayoutStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopPayoutStatusHistories_ShopPayouts_ShopPayoutId",
                        column: x => x.ShopPayoutId,
                        principalTable: "ShopPayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopPayouts_ShopId_IdempotencyKey",
                table: "ShopPayouts",
                columns: new[] { "ShopId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShopPayouts_ShopId_PeriodStartUtc_PeriodEndUtc_Currency",
                table: "ShopPayouts",
                columns: new[] { "ShopId", "PeriodStartUtc", "PeriodEndUtc", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopPayouts_Status",
                table: "ShopPayouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShopPayoutStatusHistories_ShopPayoutId_ChangedAtUtc",
                table: "ShopPayoutStatusHistories",
                columns: new[] { "ShopPayoutId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopPayoutStatusHistories_ShopPayoutId_NewStatus_IdempotencyKey",
                table: "ShopPayoutStatusHistories",
                columns: new[] { "ShopPayoutId", "NewStatus", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopPayoutStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_ShopPayouts_ShopId_IdempotencyKey",
                table: "ShopPayouts");

            migrationBuilder.DropIndex(
                name: "IX_ShopPayouts_ShopId_PeriodStartUtc_PeriodEndUtc_Currency",
                table: "ShopPayouts");

            migrationBuilder.DropIndex(
                name: "IX_ShopPayouts_Status",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ShopPayouts");

            migrationBuilder.DropColumn(
                name: "PlatformFeeMode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PlatformFeePercent",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_ShopPayouts_ShopId_PeriodStartUtc_PeriodEndUtc",
                table: "ShopPayouts",
                columns: new[] { "ShopId", "PeriodStartUtc", "PeriodEndUtc" });
        }
    }
}
