using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Живые карточки КВ (Telegram-виджеты): одна на клан и язык.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261109120000_AddWarWidgets")]
    public partial class AddWarWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarWidgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Lang = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    WidgetId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    LastData = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastShownUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPushedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_WarWidgets", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_WarWidgets_ClanId_Lang", table: "WarWidgets",
                columns: ["ClanId", "Lang"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_WarWidgets_LastShownUtc", table: "WarWidgets",
                column: "LastShownUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WarWidgets");
        }
    }
}
