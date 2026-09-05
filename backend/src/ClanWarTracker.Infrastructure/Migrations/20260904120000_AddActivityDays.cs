using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Журнал активных дней. LastVisitAtUtc хранит только последний визит, поэтому
    /// «сколько людей заходило во вторник» посчитать было нечем. Восстановить такое
    /// задним числом нельзя — можно только начать вести.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260904120000_AddActivityDays")]
    public partial class AddActivityDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Actions = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ActivityDays", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ActivityDays_PlayerId_DayUtc",
                table: "ActivityDays",
                columns: ["PlayerId", "DayUtc"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityDays_DayUtc",
                table: "ActivityDays",
                column: "DayUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "ActivityDays");
    }
}
