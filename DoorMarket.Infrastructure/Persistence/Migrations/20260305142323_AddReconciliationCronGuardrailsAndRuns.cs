using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationCronGuardrailsAndRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconciliationJobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    InProgress = table.Column<bool>(type: "bit", nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    TriggerSource = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WindowFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DraftSlaDays = table.Column<int>(type: "int", nullable: false),
                    ApprovedSlaDays = table.Column<int>(type: "int", nullable: false),
                    CandidatePayouts = table.Column<int>(type: "int", nullable: false),
                    StaleDraftCount = table.Column<int>(type: "int", nullable: false),
                    StaleApprovedCount = table.Column<int>(type: "int", nullable: false),
                    PartialPaidCount = table.Column<int>(type: "int", nullable: false),
                    PaidWithoutReferenceCount = table.Column<int>(type: "int", nullable: false),
                    OverlapPairCount = table.Column<int>(type: "int", nullable: false),
                    ReversedCount = table.Column<int>(type: "int", nullable: false),
                    InsightsCount = table.Column<int>(type: "int", nullable: false),
                    ReversalRatePercent = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    PartialGapTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationJobRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationJobRuns_InProgressUnique",
                table: "ReconciliationJobRuns",
                column: "InProgress",
                unique: true,
                filter: "[InProgress] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationJobRuns_StartedAtUtc",
                table: "ReconciliationJobRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationJobRuns_Success_StartedAtUtc",
                table: "ReconciliationJobRuns",
                columns: new[] { "Success", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationJobRuns_WasSkipped_StartedAtUtc",
                table: "ReconciliationJobRuns",
                columns: new[] { "WasSkipped", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationJobRuns");
        }
    }
}
