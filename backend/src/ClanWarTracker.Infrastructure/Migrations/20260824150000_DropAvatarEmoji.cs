using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Эмодзи-аватарки отменены: состав клана вместо них красят градиенты по заслугам
    /// (лидер, со-руководитель, король недели), и хранить выбор игрока больше незачем.
    ///
    /// Отдельной миграцией, а не правкой предыдущей: та уже применена на локальных базах,
    /// и переписывать применённую миграцию — верный способ развести схему с историей.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260824150000_DropAvatarEmoji")]
    public partial class DropAvatarEmoji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(name: "AvatarEmoji", table: "Players");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>(
                name: "AvatarEmoji", table: "Players", type: "TEXT", maxLength: 16, nullable: true);
    }
}
