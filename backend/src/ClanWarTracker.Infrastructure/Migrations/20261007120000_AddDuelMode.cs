using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Режим дружеского боя у дуэли.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261007120000_AddDuelMode")]
    public partial class AddDuelMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Mode", table: "Duels", type: "TEXT", maxLength: 24, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Mode", table: "Duels");
        }
    }
}
