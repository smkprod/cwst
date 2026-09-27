using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Оформление и девиз страницы клана.
    ///
    /// Колонки появились вместе со страницами кланов, но миграция для SQLite тогда
    /// не была добавлена — только SQL для Postgres. Прод от этого не страдал, а
    /// локальный запуск на SQLite падал на первом же запросе к кланам: EF просил
    /// колонку, которой в файле базы не было.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927120000_AddClanPageColumns")]
    public partial class AddClanPageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PageDesignKey", table: "Clans",
                type: "TEXT", maxLength: 32, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Motto", table: "Clans",
                type: "TEXT", maxLength: 160, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Motto", table: "Clans");
            migrationBuilder.DropColumn(name: "PageDesignKey", table: "Clans");
        }
    }
}
