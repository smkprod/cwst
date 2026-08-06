using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Респекты 👏 (соц. награда, 1/сутки) + снимок «последнего визита» игрока
    /// для карточки «Что нового». Designer-файла нет намеренно: атрибуты
    /// [DbContext]/[Migration] стоят прямо на классе, BuildTargetModel для
    /// применения Up() не требуется (используется только тулингом dotnet-ef).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260806120000_AddRespectsAndLastVisit")]
    public partial class AddRespectsAndLastVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastVisitAtUtc", table: "Players", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "LastVisitFame", table: "Players", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "LastVisitRank", table: "Players", type: "INTEGER", nullable: true);

            migrationBuilder.CreateTable(
                name: "Respects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromPlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    FromName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ToPlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ToName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DayUtc = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Respects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Respects_FromPlayerTag_DayUtc",
                table: "Respects",
                columns: new[] { "FromPlayerTag", "DayUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Respects_ClanId_DayUtc",
                table: "Respects",
                columns: new[] { "ClanId", "DayUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Respects_ToPlayerTag",
                table: "Respects",
                column: "ToPlayerTag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Respects");
            migrationBuilder.DropColumn(name: "LastVisitAtUtc", table: "Players");
            migrationBuilder.DropColumn(name: "LastVisitFame", table: "Players");
            migrationBuilder.DropColumn(name: "LastVisitRank", table: "Players");
        }
    }
}
