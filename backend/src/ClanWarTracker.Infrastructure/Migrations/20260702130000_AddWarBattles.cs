using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarBattles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarBattles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BattleTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Won = table.Column<bool>(type: "INTEGER", nullable: false),
                    CrownsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    CrownsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarBattles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarBattles_ClanId_PlayerTag_BattleTimeUtc",
                table: "WarBattles",
                columns: new[] { "ClanId", "PlayerTag", "BattleTimeUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarBattles_ClanId_SeasonId_SectionIndex",
                table: "WarBattles",
                columns: new[] { "ClanId", "SeasonId", "SectionIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WarBattles");
        }
    }
}
