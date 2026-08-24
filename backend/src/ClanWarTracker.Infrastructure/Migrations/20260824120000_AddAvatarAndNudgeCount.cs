using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Эмодзи-аватарка игрока (выбирается в Mini App) + счётчик полученных «пинков»
    /// для карточки дисциплины. Designer-файла нет намеренно: атрибуты
    /// [DbContext]/[Migration] стоят прямо на классе, BuildTargetModel для
    /// применения Up() не требуется (используется только тулингом dotnet-ef).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260824120000_AddAvatarAndNudgeCount")]
    public partial class AddAvatarAndNudgeCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarEmoji", table: "Players", type: "TEXT", maxLength: 16, nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "NudgeCount", table: "Players", type: "INTEGER", nullable: false, defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AvatarEmoji", table: "Players");
            migrationBuilder.DropColumn(name: "NudgeCount", table: "Players");
        }
    }
}
