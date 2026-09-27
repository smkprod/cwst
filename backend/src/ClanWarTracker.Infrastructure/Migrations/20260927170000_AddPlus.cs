using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Плюс: выдачи доступа, настройки оповещений, вид товара и возврат в журнале продаж.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927170000_AddPlus")]
    public partial class AddPlus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind", table: "SponsorPayments", type: "TEXT", maxLength: 16,
                nullable: false, defaultValue: "sponsor");
            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAtUtc", table: "SponsorPayments", type: "TEXT", nullable: true);

            migrationBuilder.CreateTable(
                name: "Entitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Days = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UntilUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ChargeId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_Entitlements", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_TelegramUserId_Sku", table: "Entitlements",
                columns: ["TelegramUserId", "Sku"]);
            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_ChargeId", table: "Entitlements", column: "ChargeId");
            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_PlayerTag", table: "Entitlements", column: "PlayerTag");

            migrationBuilder.CreateTable(
                name: "PlayerAlertPrefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    TiltAlerts = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastAlertBattleUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAlertSentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AlertDay = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    AlertsToday = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DmBlocked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                },
                constraints: table => table.PrimaryKey("PK_PlayerAlertPrefs", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAlertPrefs_TelegramUserId", table: "PlayerAlertPrefs",
                column: "TelegramUserId", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlayerAlertPrefs");
            migrationBuilder.DropTable(name: "Entitlements");
            migrationBuilder.DropColumn(name: "RefundedAtUtc", table: "SponsorPayments");
            migrationBuilder.DropColumn(name: "Kind", table: "SponsorPayments");
        }
    }
}
