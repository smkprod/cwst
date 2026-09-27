using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Журнал оплат спонсорства звёздами.
    ///
    /// Уникальный номер платежа — не ради порядка: повторная доставка того же
    /// платежа от Telegram упирается в него, и спонсорство не продлевается дважды.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927130000_AddSponsorPayments")]
    public partial class AddSponsorPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SponsorPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PayerTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Days = table.Column<int>(type: "INTEGER", nullable: false),
                    TelegramChargeId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PaidAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SponsorPayments_TelegramChargeId", table: "SponsorPayments",
                column: "TelegramChargeId", unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsorPayments_PaidAtUtc", table: "SponsorPayments",
                column: "PaidAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SponsorPayments");
        }
    }
}
