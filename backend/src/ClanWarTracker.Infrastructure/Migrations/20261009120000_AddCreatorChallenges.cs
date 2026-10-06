using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Челленджи блогеров для своих зрителей.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261009120000_AddCreatorChallenges")]
    public partial class AddCreatorChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatorTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatorName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Prize = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_CreatorChallenges", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_CreatorChallenges_Code", table: "CreatorChallenges", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_CreatorChallenges_CreatorTelegramUserId", table: "CreatorChallenges", column: "CreatorTelegramUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CreatorChallenges");
        }
    }
}
