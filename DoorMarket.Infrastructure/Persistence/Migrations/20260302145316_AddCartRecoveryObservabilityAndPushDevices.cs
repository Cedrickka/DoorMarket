using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartRecoveryObservabilityAndPushDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExperimentGroup",
                table: "AbandonedCartEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "A");

            migrationBuilder.CreateTable(
                name: "CartRecoveryJobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    CandidatesScanned = table.Column<int>(type: "int", nullable: false),
                    EventsCreated = table.Column<int>(type: "int", nullable: false),
                    RemindersSent = table.Column<int>(type: "int", nullable: false),
                    RemindersFailed = table.Column<int>(type: "int", nullable: false),
                    AntiSpamSkipped = table.Column<int>(type: "int", nullable: false),
                    ConvertedSkipped = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartRecoveryJobRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPushDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeviceModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPushDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPushDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartEvents_ExperimentGroup_DetectedAtUtc",
                table: "AbandonedCartEvents",
                columns: new[] { "ExperimentGroup", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CartRecoveryJobRuns_StartedAtUtc",
                table: "CartRecoveryJobRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CartRecoveryJobRuns_Success_StartedAtUtc",
                table: "CartRecoveryJobRuns",
                columns: new[] { "Success", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPushDevices_Platform_IsActive_LastSeenAtUtc",
                table: "UserPushDevices",
                columns: new[] { "Platform", "IsActive", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPushDevices_Token",
                table: "UserPushDevices",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPushDevices_UserId_IsActive_LastSeenAtUtc",
                table: "UserPushDevices",
                columns: new[] { "UserId", "IsActive", "LastSeenAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartRecoveryJobRuns");

            migrationBuilder.DropTable(
                name: "UserPushDevices");

            migrationBuilder.DropIndex(
                name: "IX_AbandonedCartEvents_ExperimentGroup_DetectedAtUtc",
                table: "AbandonedCartEvents");

            migrationBuilder.DropColumn(
                name: "ExperimentGroup",
                table: "AbandonedCartEvents");
        }
    }
}
