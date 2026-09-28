using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Бои топа колода против колоды — для статистики матчапа в разборе боя.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260930120000_AddMetaBattles")]
    public partial class AddMetaBattles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetaBattles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DeckA = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DeckB = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                },
                constraints: table => table.PrimaryKey("PK_MetaBattles", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_MetaBattles_DayUtc", table: "MetaBattles", column: "DayUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MetaBattles");
        }
    }
}
