using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutAlertIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutAlertIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ObservedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FirstTriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggerCount = table.Column<int>(type: "int", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AcknowledgementNote = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNotifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotificationCount = table.Column<int>(type: "int", nullable: false),
                    LastNotificationError = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutAlertIncidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAlertIncidents_AlertCode_Provider_ResolvedAtUtc",
                table: "CheckoutAlertIncidents",
                columns: new[] { "AlertCode", "Provider", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAlertIncidents_IsAcknowledged_ResolvedAtUtc_LastTriggeredAtUtc",
                table: "CheckoutAlertIncidents",
                columns: new[] { "IsAcknowledged", "ResolvedAtUtc", "LastTriggeredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAlertIncidents_Severity_ResolvedAtUtc_LastTriggeredAtUtc",
                table: "CheckoutAlertIncidents",
                columns: new[] { "Severity", "ResolvedAtUtc", "LastTriggeredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutAlertIncidents");
        }
    }
}
