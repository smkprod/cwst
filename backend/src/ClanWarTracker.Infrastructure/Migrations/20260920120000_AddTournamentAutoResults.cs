using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Автозачёт результатов матчей по боевому логу и объявления о них.
    ///
    /// ReadyAtUtc — момент, с которого бои считаются этим матчем. У матчей, созданных
    /// до этой миграции, он пуст: такие бот не трогает и оставляет организатору, потому
    /// что без границы по времени он мог бы засчитать случайную встречу тех же соперников.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260920120000_AddTournamentAutoResults")]
    public partial class AddTournamentAutoResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoResults", table: "Tournaments",
                type: "INTEGER", nullable: false, defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AnnounceResults", table: "Tournaments",
                type: "INTEGER", nullable: false, defaultValue: true);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "ReadyAtUtc", table: "TournamentMatches",
                type: "TEXT", nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoResolved", table: "TournamentMatches",
                type: "INTEGER", nullable: false, defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AutoResults", table: "Tournaments");
            migrationBuilder.DropColumn(name: "AnnounceResults", table: "Tournaments");
            migrationBuilder.DropColumn(name: "ReadyAtUtc", table: "TournamentMatches");
            migrationBuilder.DropColumn(name: "AutoResolved", table: "TournamentMatches");
        }
    }
}
