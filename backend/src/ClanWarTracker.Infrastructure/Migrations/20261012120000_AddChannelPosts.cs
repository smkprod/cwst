using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>Канал бота: опубликованные посты и черновики новостей.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261012120000_AddChannelPosts")]
    public partial class AddChannelPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    PhotoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LinkUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ButtonText = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    ButtonUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_ChannelPosts", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_ChannelPosts_SourceKey", table: "ChannelPosts", column: "SourceKey", unique: true);
            migrationBuilder.CreateIndex(name: "IX_ChannelPosts_State_CreatedUtc", table: "ChannelPosts", columns: ["State", "CreatedUtc"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChannelPosts");
        }
    }
}
