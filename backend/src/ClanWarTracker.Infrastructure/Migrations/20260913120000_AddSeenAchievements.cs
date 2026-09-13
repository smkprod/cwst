using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Снимок уровней наград на момент последнего просмотра. По нему ловится момент
    /// «открыл новую ачивку» — без него рост уровня остаётся незамеченным событием.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260913120000_AddSeenAchievements")]
    public partial class AddSeenAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>(
                name: "SeenAchievementsJson",
                table: "Players",
                type: "TEXT",
                nullable: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: "SeenAchievementsJson", table: "Players");
    }
}
