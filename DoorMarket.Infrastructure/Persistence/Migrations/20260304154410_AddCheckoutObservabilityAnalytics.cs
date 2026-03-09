using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutObservabilityAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutAnalyticsEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentProvider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PaymentChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExperimentName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExperimentGroup = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryTag = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutAnalyticsEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_EventType_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_ExperimentName_ExperimentGroup_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "ExperimentName", "ExperimentGroup", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_OrderId_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "OrderId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_PaymentProvider_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "PaymentProvider", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_SessionId_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "SessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_Source_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "Source", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_Success_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "Success", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAnalyticsEvents_UserId_OccurredAtUtc",
                table: "CheckoutAnalyticsEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutAnalyticsEvents");
        }
    }
}
