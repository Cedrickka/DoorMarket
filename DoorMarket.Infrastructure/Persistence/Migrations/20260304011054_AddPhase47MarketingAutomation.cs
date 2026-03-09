using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase47MarketingAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketingSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriteriaJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SegmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChannelEmail = table.Column<bool>(type: "bit", nullable: false),
                    ChannelPush = table.Column<bool>(type: "bit", nullable: false),
                    ChannelInApp = table.Column<bool>(type: "bit", nullable: false),
                    MessageTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MessageBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketingCampaigns_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketingCampaigns_MarketingSegments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "MarketingSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MarketingCampaignRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    TargetUsers = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    RevenueAttributed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCampaignRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketingCampaignRuns_MarketingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MarketingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaignRuns_CampaignId_StartedAtUtc",
                table: "MarketingCampaignRuns",
                columns: new[] { "CampaignId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaignRuns_Status_StartedAtUtc",
                table: "MarketingCampaignRuns",
                columns: new[] { "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaigns_CouponId",
                table: "MarketingCampaigns",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaigns_SegmentId",
                table: "MarketingCampaigns",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaigns_Status_StartAtUtc",
                table: "MarketingCampaigns",
                columns: new[] { "Status", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingSegments_IsActive_IsSystem",
                table: "MarketingSegments",
                columns: new[] { "IsActive", "IsSystem" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingSegments_Name",
                table: "MarketingSegments",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketingCampaignRuns");

            migrationBuilder.DropTable(
                name: "MarketingCampaigns");

            migrationBuilder.DropTable(
                name: "MarketingSegments");
        }
    }
}
