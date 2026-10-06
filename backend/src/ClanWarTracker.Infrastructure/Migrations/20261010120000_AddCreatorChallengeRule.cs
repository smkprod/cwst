using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Формат челленджа блогера.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261010120000_AddCreatorChallengeRule")]
    public partial class AddCreatorChallengeRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Rule", table: "CreatorChallenges", type: "TEXT", maxLength: 16,
                nullable: false, defaultValue: "tickets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Rule", table: "CreatorChallenges");
        }
    }
}
