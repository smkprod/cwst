using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>«Стоп-тильт» v2: правила и пауза в настройках, журнал сигналов, даритель у выдачи.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260928120000_AddStopTiltV2")]
    public partial class AddStopTiltV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Lang", table: "PlayerAlertPrefs", type: "TEXT", maxLength: 8, nullable: true);
            migrationBuilder.AddColumn<int>(name: "TzOffsetMinutes", table: "PlayerAlertPrefs", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FreeSignalsLeft", table: "PlayerAlertPrefs", type: "INTEGER", nullable: false, defaultValue: 2);
            migrationBuilder.AddColumn<int>(name: "LossThreshold", table: "PlayerAlertPrefs", type: "INTEGER", nullable: false, defaultValue: 2);
            migrationBuilder.AddColumn<int>(name: "DailyLossLimit", table: "PlayerAlertPrefs", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "QuietHours", table: "PlayerAlertPrefs", type: "INTEGER", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<DateTime>(name: "PauseUntilUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "MutedUntilUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "IntroSentUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "LimitAlertDay", table: "PlayerAlertPrefs", type: "TEXT", maxLength: 10, nullable: true);

            migrationBuilder.AddColumn<long>(name: "GiverTelegramUserId", table: "Entitlements", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ReminderSentUtc", table: "Entitlements", type: "TEXT", nullable: true);

            migrationBuilder.CreateTable(
                name: "TiltAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SentUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TriggerBattleUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SessionStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LossStreak = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    Free = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Lang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false, defaultValue: "ru"),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    Choice = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    ChoiceUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResumeSentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SummaryUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SessionWins = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SessionLosses = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SessionTrophies = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AfterWins = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AfterLosses = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                },
                constraints: table => table.PrimaryKey("PK_TiltAlerts", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_TiltAlerts_TelegramUserId_SentUtc", table: "TiltAlerts",
                columns: ["TelegramUserId", "SentUtc"]);
            migrationBuilder.CreateIndex(name: "IX_TiltAlerts_SentUtc", table: "TiltAlerts", column: "SentUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TiltAlerts");
            foreach (var c in new[] { "Lang", "TzOffsetMinutes", "FreeSignalsLeft", "LossThreshold", "DailyLossLimit",
                         "QuietHours", "PauseUntilUtc", "MutedUntilUtc", "IntroSentUtc", "LimitAlertDay" })
                migrationBuilder.DropColumn(name: c, table: "PlayerAlertPrefs");
            migrationBuilder.DropColumn(name: "GiverTelegramUserId", table: "Entitlements");
            migrationBuilder.DropColumn(name: "ReminderSentUtc", table: "Entitlements");
        }
    }
}
