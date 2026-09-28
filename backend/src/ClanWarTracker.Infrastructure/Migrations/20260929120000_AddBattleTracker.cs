using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Трекер боёв: подробности боя в истории и состояние карточки захода в настройках.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260929120000_AddBattleTracker")]
    public partial class AddBattleTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "OppTag", table: "PlayerBattles", type: "TEXT", maxLength: 16, nullable: true);
            migrationBuilder.AddColumn<string>(name: "OppName", table: "PlayerBattles", type: "TEXT", maxLength: 32, nullable: true);
            migrationBuilder.AddColumn<int>(name: "GameModeId", table: "PlayerBattles", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DeckSelection", table: "PlayerBattles", type: "TEXT", maxLength: 24, nullable: true);
            migrationBuilder.AddColumn<string>(name: "OppArchetype", table: "PlayerBattles", type: "TEXT", maxLength: 24, nullable: true);
            migrationBuilder.AddColumn<double>(name: "LevelGap", table: "PlayerBattles", type: "REAL", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DetailJson", table: "PlayerBattles", type: "TEXT", nullable: true);

            migrationBuilder.AddColumn<bool>(name: "TrackerEnabled", table: "PlayerAlertPrefs", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "TrackerWatermarkUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TrackerCardMessageId", table: "PlayerAlertPrefs", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "TrackerCardStartUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "TrackerMutedUntilUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "TrackerHintBattleUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in new[] { "OppTag", "OppName", "GameModeId", "DeckSelection", "OppArchetype", "LevelGap", "DetailJson" })
                migrationBuilder.DropColumn(name: c, table: "PlayerBattles");
            foreach (var c in new[] { "TrackerEnabled", "TrackerWatermarkUtc", "TrackerCardMessageId", "TrackerCardStartUtc",
                         "TrackerMutedUntilUtc", "TrackerHintBattleUtc" })
                migrationBuilder.DropColumn(name: c, table: "PlayerAlertPrefs");
        }
    }
}
