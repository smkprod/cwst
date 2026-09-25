using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Витрина значка и спонсорство — первые вещи в проекте, которые игрок
    /// показывает другим, а не смотрит сам.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260925120000_AddSponsorAndShowcase")]
    public partial class AddSponsorAndShowcase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShowcaseBadgeKey", table: "Players", type: "TEXT", maxLength: 32, nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "ShowcaseBadgeLevel", table: "Players", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<System.DateTime>(
                name: "SponsorUntilUtc", table: "Players", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "SponsorBackgroundKey", table: "Players", type: "TEXT", maxLength: 32, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShowcaseBadgeKey", table: "Players");
            migrationBuilder.DropColumn(name: "ShowcaseBadgeLevel", table: "Players");
            migrationBuilder.DropColumn(name: "SponsorUntilUtc", table: "Players");
            migrationBuilder.DropColumn(name: "SponsorBackgroundKey", table: "Players");
        }
    }
}
