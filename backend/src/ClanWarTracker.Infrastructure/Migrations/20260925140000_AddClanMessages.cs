using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Сообщения между кланами и выключатель входящих.
    ///
    /// Таблица нужна не ради переписки, а ради ограничения: «раз в сутки на пару»
    /// проверяется только по записям. Выключатель — чтобы у недовольного был
    /// выход, не доходящий до жалобы на бота.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260925140000_AddClanMessages")]
    public partial class AddClanMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsClanMail", table: "Clans",
                type: "INTEGER", nullable: false, defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ClanMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    SentByTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    SentByName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SentAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanMessages", x => x.Id);
                    table.ForeignKey("FK_ClanMessages_Clans_FromClanId", x => x.FromClanId,
                        "Clans", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_ClanMessages_Clans_ToClanId", x => x.ToClanId,
                        "Clans", "Id", onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClanMessages_Pair", table: "ClanMessages",
                columns: ["FromClanId", "ToClanId", "SentAtUtc"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ClanMessages");
            migrationBuilder.DropColumn(name: "AcceptsClanMail", table: "Clans");
        }
    }
}
