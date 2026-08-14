using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Метка источника данных недели: "live" (снято с текущей войны) или "log"
    /// (подтверждённый финал из /riverracelog). Агрегаты предпочитают "log".
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260806140000_AddWarSnapshotSource")]
    public partial class AddWarSnapshotSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "WarSnapshots",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "live");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Source", table: "WarSnapshots");
        }
    }
}
