using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Лига дуэлей 1х1: профили с рейтингом и принятые вызовы.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261006120000_AddDuels")]
    public partial class AddDuels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuelProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FriendLink = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1000),
                    Peak = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1000),
                    Games = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Lang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    JoinedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastDuelUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_DuelProfiles", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_DuelProfiles_TelegramUserId", table: "DuelProfiles",
                column: "TelegramUserId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_DuelProfiles_Rating", table: "DuelProfiles", column: "Rating");

            migrationBuilder.CreateTable(
                name: "Duels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    BestOf = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    ATelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ATag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    BTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScoreA = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ScoreB = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RatingA = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RatingB = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DeltaA = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DeltaB = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Rated = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AcceptedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CheckedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChatId = table.Column<long>(type: "INTEGER", nullable: true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    InlineMessageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Lang = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_Duels", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_Duels_InlineMessageId", table: "Duels",
                column: "InlineMessageId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Duels_ChatId_MessageId", table: "Duels",
                columns: ["ChatId", "MessageId"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_Duels_State", table: "Duels", column: "State");
            migrationBuilder.CreateIndex(name: "IX_Duels_ATelegramUserId", table: "Duels", column: "ATelegramUserId");
            migrationBuilder.CreateIndex(name: "IX_Duels_BTelegramUserId", table: "Duels", column: "BTelegramUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Duels");
            migrationBuilder.DropTable(name: "DuelProfiles");
        }
    }
}
