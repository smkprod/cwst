using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Воронка: откуда пришли люди (реклама или реферал) и какие кампании заведены.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927140000_AddAcquisitionsAndCampaigns")]
    public partial class AddAcquisitionsAndCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acquisitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReferrerTelegramUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    StartedAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: false),
                    LinkedAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: true),
                    ClanConnectedAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_Acquisitions", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Acquisitions_TelegramUserId", table: "Acquisitions",
                column: "TelegramUserId", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Acquisitions_Source", table: "Acquisitions", column: "Source");

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_Campaigns", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Code", table: "Campaigns", column: "Code", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Campaigns");
            migrationBuilder.DropTable(name: "Acquisitions");
        }
    }
}
