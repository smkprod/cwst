using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "Tournaments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "Tournaments");
        }
    }
}
