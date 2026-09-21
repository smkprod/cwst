using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Уникальный индекс по чату клана становится частичным.
    ///
    /// Ноль в TelegramChatId означает «чат не привязан»: бот заводит клан сам, когда
    /// игрок присылает свой тег в личку, чтобы приложение показало войну без участия
    /// главы. Таких кланов много, а сплошной уникальный индекс допускал ровно один —
    /// второй падал с duplicate key, и сырая ошибка Postgres уходила игроку в ответ.
    ///
    /// Настоящие chat id нулём не бывают, так что уникальность привязанных чатов
    /// фильтр не ослабляет.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260921120000_PartialClanChatIndex")]
    public partial class PartialClanChatIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Clans_TelegramChatId", table: "Clans");
            migrationBuilder.CreateIndex(
                name: "IX_Clans_TelegramChatId", table: "Clans", column: "TelegramChatId",
                unique: true, filter: "\"TelegramChatId\" <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Clans_TelegramChatId", table: "Clans");
            migrationBuilder.CreateIndex(
                name: "IX_Clans_TelegramChatId", table: "Clans", column: "TelegramChatId",
                unique: true);
        }
    }
}
