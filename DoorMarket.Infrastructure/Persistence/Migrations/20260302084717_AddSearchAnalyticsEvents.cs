using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchAnalyticsEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchAnalyticsEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    NormalizedQuery = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TargetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: true),
                    ResultsCount = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Page = table.Column<int>(type: "int", nullable: true),
                    Sort = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FiltersHash = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CountryTag = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchAnalyticsEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchAnalyticsEvents_EventType_OccurredAtUtc",
                table: "SearchAnalyticsEvents",
                columns: new[] { "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchAnalyticsEvents_NormalizedQuery_EventType_OccurredAtUtc",
                table: "SearchAnalyticsEvents",
                columns: new[] { "NormalizedQuery", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchAnalyticsEvents_SessionId_OccurredAtUtc",
                table: "SearchAnalyticsEvents",
                columns: new[] { "SessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchAnalyticsEvents_Source_EventType_OccurredAtUtc",
                table: "SearchAnalyticsEvents",
                columns: new[] { "Source", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchAnalyticsEvents_UserId_OccurredAtUtc",
                table: "SearchAnalyticsEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchAnalyticsEvents");
        }
    }
}
