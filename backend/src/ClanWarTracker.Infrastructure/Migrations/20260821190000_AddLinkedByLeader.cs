using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Пометка «привязал лидер», а не сам игрок (команда /bind в чате клана).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260821190000_AddLinkedByLeader")]
    public partial class AddLinkedByLeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<bool>(
                name: "LinkedByLeader", table: "Players", type: "INTEGER", nullable: false, defaultValue: false);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: "LinkedByLeader", table: "Players");
    }
}
