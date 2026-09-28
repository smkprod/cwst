using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Участники уикенд-челленджа.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261001120000_AddChallenge")]
    public partial class AddChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChallengeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    JoinedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ChallengeEntries", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_ChallengeEntries_EventId_TelegramUserId", table: "ChallengeEntries",
                columns: ["EventId", "TelegramUserId"], unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChallengeEntries");
        }
    }
}
