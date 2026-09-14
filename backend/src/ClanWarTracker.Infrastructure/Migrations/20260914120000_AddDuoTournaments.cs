using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Парные турниры (2×2): формат и дата старта у турнира, команда у участника.
    /// Существующие турниры остаются одиночными — Mode по умолчанию 0.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260914120000_AddDuoTournaments")]
    public partial class AddDuoTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode", table: "Tournaments", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAtUtc", table: "Tournaments", type: "TEXT", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamName", table: "TournamentParticipants", type: "TEXT", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PartnerPlayerTag", table: "TournamentParticipants", type: "TEXT", maxLength: 16, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "PartnerPlayerName", table: "TournamentParticipants", type: "TEXT", maxLength: 64, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Mode", table: "Tournaments");
            migrationBuilder.DropColumn(name: "StartsAtUtc", table: "Tournaments");
            migrationBuilder.DropColumn(name: "TeamName", table: "TournamentParticipants");
            migrationBuilder.DropColumn(name: "PartnerPlayerTag", table: "TournamentParticipants");
            migrationBuilder.DropColumn(name: "PartnerPlayerName", table: "TournamentParticipants");
        }
    }
}
