using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAbandonedCartEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbandonedCartEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastCartActivityAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReminderStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReminderAttemptCount = table.Column<int>(type: "int", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SentChannels = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    ReminderSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbandonedCartEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbandonedCartEvents_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbandonedCartEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartEvents_CartId_DetectedAtUtc",
                table: "AbandonedCartEvents",
                columns: new[] { "CartId", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartEvents_ReminderStatus_DetectedAtUtc",
                table: "AbandonedCartEvents",
                columns: new[] { "ReminderStatus", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartEvents_UserId_DetectedAtUtc",
                table: "AbandonedCartEvents",
                columns: new[] { "UserId", "DetectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbandonedCartEvents");
        }
    }
}
