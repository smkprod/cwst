using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TournamentTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatorTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatorName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTournaments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameTournaments_TournamentTag",
                table: "GameTournaments",
                column: "TournamentTag",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GameTournaments");
        }
    }
}
