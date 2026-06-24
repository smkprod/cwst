using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentMinParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinParticipants",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinParticipants",
                table: "Tournaments");
        }
    }
}
