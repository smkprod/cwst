using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Шаги к покупке: увидел предложение, нажал «Купить».</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261019120000_AddFunnelEvents")]
    public partial class AddFunnelEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FunnelEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Product = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Step = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Place = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    FirstUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_FunnelEvents", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_FunnelEvents_TelegramUserId_DayUtc_Product_Step_Place", table: "FunnelEvents",
                columns: ["TelegramUserId", "DayUtc", "Product", "Step", "Place"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_FunnelEvents_DayUtc", table: "FunnelEvents", column: "DayUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FunnelEvents");
        }
    }
}
