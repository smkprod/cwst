using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Суточные снимки мирового топа. API отдаёт рейтинг только на «сейчас», поэтому
    /// вся динамика существует лишь постольку, поскольку мы её копим.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260915120000_AddTopPlayers")]
    public partial class AddTopPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TopPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClanName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Trophies = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ExpLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DeckCardIds = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_TopPlayers", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_TopPlayers_DayUtc_Rank", table: "TopPlayers",
                columns: ["DayUtc", "Rank"], unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_TopPlayers_DayUtc", table: "TopPlayers", column: "DayUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "TopPlayers");
    }
}
