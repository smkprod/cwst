using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Модераторы сервиса — первая в проекте таблица прав.
    ///
    /// До неё права нигде не хранились: владелец брался из конфига, админ чата
    /// спрашивался у Telegram, лидер клана — у Clash Royale. Модератора спросить
    /// не у кого, поэтому его приходится помнить самим.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260924140000_AddServiceModerators")]
    public partial class AddServiceModerators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceModerators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramUsername = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddedAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: false),
                    AddedByTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstSeenAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_ServiceModerators", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ServiceModerators_TelegramUsername", table: "ServiceModerators",
                column: "TelegramUsername", unique: true);

            // Частичный: неподтверждённых записей с пустым id может быть сколько угодно.
            migrationBuilder.CreateIndex(
                name: "IX_ServiceModerators_TelegramUserId", table: "ServiceModerators",
                column: "TelegramUserId", unique: true, filter: "\"TelegramUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "ServiceModerators");
    }
}
