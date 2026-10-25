using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Разведка+: ход военного дня кланов гонки и дни их игроков.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024120000_AddRaceTicks")]
    public partial class AddRaceTicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaceTicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ClanTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    DayIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Fame = table.Column<int>(type: "INTEGER", nullable: false),
                    DecksUsedToday = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveToday = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_RaceTicks", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_RaceTicks_ClanTag_SlotUtc", table: "RaceTicks",
                columns: ["ClanTag", "SlotUtc"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_RaceTicks_SlotUtc", table: "RaceTicks", column: "SlotUtc");

            migrationBuilder.CreateTable(
                name: "RacePlayerDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ClanTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    DayIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Decks = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekFame = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstPlayUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPlayUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_RacePlayerDays", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_RacePlayerDays_ClanTag_SeasonId_SectionIndex_DayIndex_PlayerTag",
                table: "RacePlayerDays", columns: ["ClanTag", "SeasonId", "SectionIndex", "DayIndex", "PlayerTag"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_RacePlayerDays_UpdatedUtc", table: "RacePlayerDays", column: "UpdatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RaceTicks");
            migrationBuilder.DropTable(name: "RacePlayerDays");
        }
    }
}
