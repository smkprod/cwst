using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Отказ от утреннего дайджеста: кнопка «Не присылать» под ним.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261011120000_AddDigestOff")]
    public partial class AddDigestOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<bool>(
                name: "DigestOff", table: "PlayerAlertPrefs", type: "INTEGER", nullable: false, defaultValue: false);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: "DigestOff", table: "PlayerAlertPrefs");
    }
}
