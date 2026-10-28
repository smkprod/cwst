using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Оплата через банку Монобанка: заказы с кодом и переводы из выписки.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261028120000_AddJarPayments")]
    public partial class AddJarPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JarOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Product = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Days = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountUah = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaidUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PaidKop = table.Column<long>(type: "INTEGER", nullable: true),
                    Manual = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_JarOrders", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_JarOrders_Code", table: "JarOrders", column: "Code");
            migrationBuilder.CreateIndex(name: "IX_JarOrders_TelegramUserId_CreatedUtc", table: "JarOrders",
                columns: ["TelegramUserId", "CreatedUtc"]);
            migrationBuilder.CreateIndex(name: "IX_JarOrders_CreatedUtc", table: "JarOrders", column: "CreatedUtc");

            migrationBuilder.CreateTable(
                name: "JarIncomings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    MonoTxId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AmountKop = table.Column<long>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Sender = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_JarIncomings", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_JarIncomings_MonoTxId", table: "JarIncomings", column: "MonoTxId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_JarIncomings_TimeUtc", table: "JarIncomings", column: "TimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "JarOrders");
            migrationBuilder.DropTable(name: "JarIncomings");
        }
    }
}
