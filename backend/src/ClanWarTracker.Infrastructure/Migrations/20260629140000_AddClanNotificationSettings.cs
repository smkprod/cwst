using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClanNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationSettingsJson",
                table: "Clans",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationSettingsJson",
                table: "Clans");
        }
    }
}
