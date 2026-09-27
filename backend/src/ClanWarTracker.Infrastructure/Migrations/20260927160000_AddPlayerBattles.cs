using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Бои привязанных игроков для личного разбора.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927160000_AddPlayerBattles")]
    public partial class AddPlayerBattles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerBattles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BattleTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CrownsFor = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CrownsAgainst = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DeckKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OppDeckKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ElixirLeaked = table.Column<double>(type: "REAL", nullable: true),
                    TrophyChange = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_PlayerBattles", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattles_PlayerTag_BattleTimeUtc", table: "PlayerBattles",
                columns: ["PlayerTag", "BattleTimeUtc"], unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattles_BattleTimeUtc", table: "PlayerBattles", column: "BattleTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "PlayerBattles");
    }
}
