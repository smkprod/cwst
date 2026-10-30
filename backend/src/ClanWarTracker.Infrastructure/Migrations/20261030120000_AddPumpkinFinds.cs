using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Охота на тыквы: кто какие тыквы нашёл.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261030120000_AddPumpkinFinds")]
    public partial class AddPumpkinFinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PumpkinFinds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PumpkinId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoundUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_PumpkinFinds", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_PumpkinFinds_TelegramUserId_PumpkinId", table: "PumpkinFinds",
                columns: ["TelegramUserId", "PumpkinId"], unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PumpkinFinds");
        }
    }
}
