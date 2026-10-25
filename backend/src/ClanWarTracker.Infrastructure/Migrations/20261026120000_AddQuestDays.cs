using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Задания дня: что выпало, что закрыто, серия; отказ от вечернего напоминания о серии.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261026120000_AddQuestDays")]
    public partial class AddQuestDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ClanId = table.Column<int>(type: "INTEGER", nullable: true),
                    Day = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Quests = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DoneMask = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Streak = table.Column<int>(type: "INTEGER", nullable: false),
                    RewardDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_QuestDays", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_QuestDays_TelegramUserId_Day", table: "QuestDays",
                columns: ["TelegramUserId", "Day"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_QuestDays_ClanId_Day", table: "QuestDays", columns: ["ClanId", "Day"]);
            migrationBuilder.CreateIndex(name: "IX_QuestDays_Day", table: "QuestDays", column: "Day");

            migrationBuilder.AddColumn<bool>(
                name: "QuestRemindOff", table: "PlayerAlertPrefs", type: "INTEGER", nullable: false, defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QuestDays");
            migrationBuilder.DropColumn(name: "QuestRemindOff", table: "PlayerAlertPrefs");
        }
    }
}
