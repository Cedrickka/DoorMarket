using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicCommissionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScopeShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    PlatformFeeMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PlatformFeePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    MinUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionRules_Categories_ScopeCategoryId",
                        column: x => x.ScopeCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionRules_Products_ScopeProductId",
                        column: x => x.ScopeProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionRules_Shops_ScopeShopId",
                        column: x => x.ScopeShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_IsActive_ScopeType_Priority",
                table: "CommissionRules",
                columns: new[] { "IsActive", "ScopeType", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_ScopeCategoryId",
                table: "CommissionRules",
                column: "ScopeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_ScopeProductId",
                table: "CommissionRules",
                column: "ScopeProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_ScopeShopId_ScopeCategoryId_ScopeProductId_IsActive",
                table: "CommissionRules",
                columns: new[] { "ScopeShopId", "ScopeCategoryId", "ScopeProductId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRules_StartsAtUtc_EndsAtUtc",
                table: "CommissionRules",
                columns: new[] { "StartsAtUtc", "EndsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionRules");
        }
    }
}
