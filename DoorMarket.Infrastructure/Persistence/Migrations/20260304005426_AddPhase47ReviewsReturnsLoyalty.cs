using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase47ReviewsReturnsLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HelpfulCount",
                table: "ShopReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrlsJson",
                table: "ShopReviews",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrlsJson",
                table: "ShopReviews",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangedAtUtc",
                table: "ReturnRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "ReturnRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaTargetAtUtc",
                table: "ReturnRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    EarnPointsPerUsd = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MinOrderAmountUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RedeemValueUsdPerPoint = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    MinRedeemPoints = table.Column<int>(type: "int", nullable: false),
                    MaxRedeemPercentOfOrder = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PointsBalance = table.Column<int>(type: "int", nullable: false),
                    LifetimePointsEarned = table.Column<int>(type: "int", nullable: false),
                    LifetimePointsSpent = table.Column<int>(type: "int", nullable: false),
                    MonetaryBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyWallets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TitleFr = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    DescriptionFr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultSlaHours = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopReviewHelpfulVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsHelpful = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopReviewHelpfulVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopReviewHelpfulVotes_ShopReviews_ShopReviewId",
                        column: x => x.ShopReviewId,
                        principalTable: "ShopReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShopReviewHelpfulVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPointLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DeltaPoints = table.Column<int>(type: "int", nullable: false),
                    DeltaAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfterPoints = table.Column<int>(type: "int", nullable: false),
                    BalanceAfterAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPointLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyPointLedgers_LoyaltyWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "LoyaltyWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoyaltyPointLedgers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ReturnReasons",
                columns: new[] { "Id", "Code", "TitleFr", "TitleEn", "DescriptionFr", "DescriptionEn", "DefaultSlaHours", "IsActive", "SortOrder", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("6A4AE22C-9553-4A0D-BC2D-10386FD3E101"), "DAMAGED", "Produit endommage", "Damaged product", "Produit recu casse, defectueux ou altere.", "Received item is damaged or defective.", 48, true, 10, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6A4AE22C-9553-4A0D-BC2D-10386FD3E102"), "NOT_AS_DESCRIBED", "Non conforme a la description", "Not as described", "Le produit ne correspond pas a la fiche.", "Product differs from listing.", 72, true, 20, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6A4AE22C-9553-4A0D-BC2D-10386FD3E103"), "MISSING_PARTS", "Article incomplet", "Missing parts", "Accessoires ou pieces manquants.", "Missing accessories or components.", 72, true, 30, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6A4AE22C-9553-4A0D-BC2D-10386FD3E104"), "WRONG_ITEM", "Mauvais article recu", "Wrong item received", "Article livre different de la commande.", "Delivered item does not match order.", 48, true, 40, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6A4AE22C-9553-4A0D-BC2D-10386FD3E105"), "QUALITY_ISSUE", "Probleme de qualite", "Quality issue", "Probleme qualite apres ouverture.", "Quality concern after opening/using item.", 96, true, 50, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.InsertData(
                table: "LoyaltyRules",
                columns: new[] { "Id", "Name", "IsActive", "Priority", "EarnPointsPerUsd", "MinOrderAmountUsd", "RedeemValueUsdPerPoint", "MinRedeemPoints", "MaxRedeemPercentOfOrder", "Notes", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { new Guid("D5E6D76E-8AD7-44E6-B3FC-315120F77A01"), "Default Loyalty Rule", true, 100, 1.0000m, 0m, 0.010000m, 100, 25.00m, "Default earn/redeem baseline for Phase 47.", new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_SlaTargetAtUtc_Status",
                table: "ReturnRequests",
                columns: new[] { "SlaTargetAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointLedgers_SourceType_SourceId",
                table: "LoyaltyPointLedgers",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointLedgers_UserId_CreatedAtUtc",
                table: "LoyaltyPointLedgers",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPointLedgers_WalletId",
                table: "LoyaltyPointLedgers",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRules_IsActive_Priority",
                table: "LoyaltyRules",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyWallets_UserId",
                table: "LoyaltyWallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReasons_Code",
                table: "ReturnReasons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReasons_IsActive_SortOrder",
                table: "ReturnReasons",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopReviewHelpfulVotes_ShopReviewId_IsHelpful",
                table: "ShopReviewHelpfulVotes",
                columns: new[] { "ShopReviewId", "IsHelpful" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopReviewHelpfulVotes_ShopReviewId_UserId",
                table: "ShopReviewHelpfulVotes",
                columns: new[] { "ShopReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopReviewHelpfulVotes_UserId",
                table: "ShopReviewHelpfulVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyPointLedgers");

            migrationBuilder.DropTable(
                name: "LoyaltyRules");

            migrationBuilder.DropTable(
                name: "ReturnReasons");

            migrationBuilder.DropTable(
                name: "ShopReviewHelpfulVotes");

            migrationBuilder.DropTable(
                name: "LoyaltyWallets");

            migrationBuilder.DropIndex(
                name: "IX_ReturnRequests_SlaTargetAtUtc_Status",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "HelpfulCount",
                table: "ShopReviews");

            migrationBuilder.DropColumn(
                name: "PhotoUrlsJson",
                table: "ShopReviews");

            migrationBuilder.DropColumn(
                name: "VideoUrlsJson",
                table: "ShopReviews");

            migrationBuilder.DropColumn(
                name: "LastStatusChangedAtUtc",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "SlaTargetAtUtc",
                table: "ReturnRequests");
        }
    }
}
