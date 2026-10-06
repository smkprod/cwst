using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Турниры для блогеров: режим дружеского боя и ключ виджетов OBS.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261008120000_AddTournamentGameModeAndOverlay")]
    public partial class AddTournamentGameModeAndOverlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "GameMode", table: "Tournaments", type: "TEXT", maxLength: 24, nullable: true);
            migrationBuilder.AddColumn<string>(name: "OverlayKey", table: "Tournaments", type: "TEXT", maxLength: 32, nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Tournaments_OverlayKey", table: "Tournaments", column: "OverlayKey", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Tournaments_OverlayKey", table: "Tournaments");
            migrationBuilder.DropColumn(name: "OverlayKey", table: "Tournaments");
            migrationBuilder.DropColumn(name: "GameMode", table: "Tournaments");
        }
    }
}
