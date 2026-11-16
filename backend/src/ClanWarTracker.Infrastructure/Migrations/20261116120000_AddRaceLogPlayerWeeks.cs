using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Недели КВ игроков из журналов войн - в том числе в чужих кланах.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261116120000_AddRaceLogPlayerWeeks")]
    public partial class AddRaceLogPlayerWeeks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaceLogPlayerWeeks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ClanTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ClanName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    IsColosseum = table.Column<bool>(type: "INTEGER", nullable: false),
                    Fame = table.Column<int>(type: "INTEGER", nullable: false),
                    DecksUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    ClanFame = table.Column<int>(type: "INTEGER", nullable: false),
                    ClanDecks = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_RaceLogPlayerWeeks", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_RaceLogPlayerWeeks_PlayerTag_ClanTag_SeasonId_SectionIndex", table: "RaceLogPlayerWeeks",
                columns: ["PlayerTag", "ClanTag", "SeasonId", "SectionIndex"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_RaceLogPlayerWeeks_ClanTag_SeasonId_SectionIndex", table: "RaceLogPlayerWeeks",
                columns: ["ClanTag", "SeasonId", "SectionIndex"]);
            migrationBuilder.CreateIndex(name: "IX_RaceLogPlayerWeeks_CreatedUtc", table: "RaceLogPlayerWeeks", column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RaceLogPlayerWeeks");
        }
    }
}
