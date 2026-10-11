using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Дневные лимиты запросов игрока к нейросети.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013120000_AddAiUsage")]
    public partial class AddAiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Feature = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Day = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_AiUsages", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_AiUsages_TelegramUserId_Feature_Day", table: "AiUsages",
                columns: ["TelegramUserId", "Feature", "Day"], unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AiUsages");
        }
    }
}
