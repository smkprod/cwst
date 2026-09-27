using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Мета дня из боёв топа: колоды с итогами и пары «карта против карты».</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927150000_AddMeta")]
    public partial class AddMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetaDeckDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DeckKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Games = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Draws = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                },
                constraints: table => table.PrimaryKey("PK_MetaDeckDays", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_MetaDeckDays_DayUtc_DeckKey", table: "MetaDeckDays",
                columns: ["DayUtc", "DeckKey"], unique: true);

            migrationBuilder.CreateTable(
                name: "MetaMatchupDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CardKey = table.Column<int>(type: "INTEGER", nullable: false),
                    OppCardKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Games = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                },
                constraints: table => table.PrimaryKey("PK_MetaMatchupDays", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_MetaMatchupDays_DayUtc_CardKey_OppCardKey", table: "MetaMatchupDays",
                columns: ["DayUtc", "CardKey", "OppCardKey"], unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MetaMatchupDays");
            migrationBuilder.DropTable(name: "MetaDeckDays");
        }
    }
}
