using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Заходы в Mini App с источником.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015120000_AddAppOpens")]
    public partial class AddAppOpens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppOpens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Dest = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Linked = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_AppOpens", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_AppOpens_TelegramUserId_DayUtc_Source_Dest", table: "AppOpens",
                columns: ["TelegramUserId", "DayUtc", "Source", "Dest"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_AppOpens_DayUtc", table: "AppOpens", column: "DayUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppOpens");
        }
    }
}
