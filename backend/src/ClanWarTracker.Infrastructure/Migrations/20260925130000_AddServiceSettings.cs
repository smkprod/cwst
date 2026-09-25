using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Настройки, которые владелец правит из панели: состав нижних вкладок и подобное.
    /// Конфиг для этого не годится — он в образе, и правка требует передеплоя.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260925130000_AddServiceSettings")]
    public partial class AddServiceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    UpdatedAtUtc = table.Column<System.DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ServiceSettings", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSettings_Key", table: "ServiceSettings", column: "Key", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "ServiceSettings");
    }
}
