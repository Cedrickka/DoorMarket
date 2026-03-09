using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoorMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalNotificationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionalNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionalNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalNotificationLogs_AttemptedAtUtc",
                table: "TransactionalNotificationLogs",
                column: "AttemptedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalNotificationLogs_NotificationType_Status_AttemptedAtUtc",
                table: "TransactionalNotificationLogs",
                columns: new[] { "NotificationType", "Status", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalNotificationLogs_OrderId_AttemptedAtUtc",
                table: "TransactionalNotificationLogs",
                columns: new[] { "OrderId", "AttemptedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionalNotificationLogs");
        }
    }
}
