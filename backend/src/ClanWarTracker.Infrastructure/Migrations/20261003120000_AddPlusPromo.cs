using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Личная скидка на Плюс: цена на себя до указанного момента.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261003120000_AddPlusPromo")]
    public partial class AddPlusPromo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "PromoPrice7", table: "PlayerAlertPrefs", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PromoPrice30", table: "PlayerAlertPrefs", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "PromoUntilUtc", table: "PlayerAlertPrefs", type: "TEXT", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in new[] { "PromoPrice7", "PromoPrice30", "PromoUntilUtc" })
                migrationBuilder.DropColumn(name: c, table: "PlayerAlertPrefs");
        }
    }
}
