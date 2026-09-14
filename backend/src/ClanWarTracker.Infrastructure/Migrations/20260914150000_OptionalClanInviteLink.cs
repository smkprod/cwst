using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Ссылка на клан турнира становится необязательной: клан обычно создают позже,
    /// ближе к дате, и требовать её заранее — значит заставлять писать заглушку.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260914150000_OptionalClanInviteLink")]
    public partial class OptionalClanInviteLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AlterColumn<string>(
                name: "ClanInviteLink", table: "Tournaments",
                type: "TEXT", maxLength: 300, nullable: true,
                oldClrType: typeof(string), oldType: "TEXT", oldMaxLength: 300, oldNullable: false);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AlterColumn<string>(
                name: "ClanInviteLink", table: "Tournaments",
                type: "TEXT", maxLength: 300, nullable: false, defaultValue: "",
                oldClrType: typeof(string), oldType: "TEXT", oldMaxLength: 300, oldNullable: true);
    }
}
