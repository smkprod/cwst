using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// «Карта дня»: результат игрока за один день. Запись создаётся на первой попытке,
    /// поэтому уникальность (PlayerId, Day) — это ещё и запрет переигрывать загадку
    /// после промаха.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260904090000_AddPuzzleResults")]
    public partial class AddPuzzleResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PuzzleResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Solved = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PlayedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_PuzzleResults", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleResults_PlayerId_Day",
                table: "PuzzleResults",
                columns: ["PlayerId", "Day"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleResults_PlayerId_Day_Solved",
                table: "PuzzleResults",
                columns: ["PlayerId", "Day", "Solved"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "PuzzleResults");
    }
}
