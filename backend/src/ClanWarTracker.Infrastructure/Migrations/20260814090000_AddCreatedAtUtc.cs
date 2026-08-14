using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClanWarTracker.Infrastructure.Persistence;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <summary>
    /// Даты подключения клана и привязки игрока — чтобы в панели владельца был виден
    /// рост (новые за 7/30 дней). Nullable: у существующих записей даты нет, и
    /// придумывать её нельзя — она бы врала в статистике.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260814090000_AddCreatedAtUtc")]
    public partial class AddCreatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc", table: "Clans", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc", table: "Players", type: "TEXT", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedAtUtc", table: "Clans");
            migrationBuilder.DropColumn(name: "CreatedAtUtc", table: "Players");
        }
    }
}
