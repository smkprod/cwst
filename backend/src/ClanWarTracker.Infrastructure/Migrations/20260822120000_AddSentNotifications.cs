using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Журнал отправленных уведомлений: дедуп переезжает из памяти воркера в БД,
    /// чтобы рестарт не превращался в повторную рассылку.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260822120000_AddSentNotifications")]
    public partial class AddSentNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SentNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_SentNotifications", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_Kind_Key",
                table: "SentNotifications",
                columns: ["Kind", "Key"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_SentAtUtc",
                table: "SentNotifications",
                column: "SentAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "SentNotifications");
    }
}
