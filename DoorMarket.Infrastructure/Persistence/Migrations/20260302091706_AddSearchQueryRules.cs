using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchQueryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchQueryRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggerQuery = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CanonicalQuery = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TargetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchQueryRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueryRules_IsActive_TriggerQuery",
                table: "SearchQueryRules",
                columns: new[] { "IsActive", "TriggerQuery" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueryRules_TargetType_TargetId_IsActive",
                table: "SearchQueryRules",
                columns: new[] { "TargetType", "TargetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueryRules_TriggerQuery",
                table: "SearchQueryRules",
                column: "TriggerQuery",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchQueryRules");
        }
    }
}
