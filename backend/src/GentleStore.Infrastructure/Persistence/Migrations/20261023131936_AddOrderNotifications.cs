using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GentleStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramLanguage",
                table: "Users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramLinkTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramLinkTokenHash",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Event = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderNotifications_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramChatId",
                table: "Users",
                column: "TelegramChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramLinkTokenHash",
                table: "Users",
                column: "TelegramLinkTokenHash",
                unique: true,
                filter: "\"TelegramLinkTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId_PaidAt",
                table: "Orders",
                columns: new[] { "StoreId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderNotifications_OrderId_UserId_Event",
                table: "OrderNotifications",
                columns: new[] { "OrderId", "UserId", "Event" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderNotifications_Status_NextAttemptAt",
                table: "OrderNotifications",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderNotifications_UserId",
                table: "OrderNotifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Users_TelegramChatId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TelegramLinkTokenHash",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StoreId_PaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramLanguage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkTokenHash",
                table: "Users");
        }
    }
}
