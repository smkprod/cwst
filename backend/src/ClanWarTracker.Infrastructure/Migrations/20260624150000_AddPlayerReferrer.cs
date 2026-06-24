using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerReferrer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReferrerTelegramUserId",
                table: "Players",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferrerTelegramUserId",
                table: "Players");
        }
    }
}
