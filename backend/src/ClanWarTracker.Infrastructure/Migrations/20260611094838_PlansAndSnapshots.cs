using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlansAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAtUtc",
                table: "Clans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanTier",
                table: "Clans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WarSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalFame = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalDecksUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarSnapshots_Clans_ClanId",
                        column: x => x.ClanId,
                        principalTable: "Clans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerWarSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WarSnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Fame = table.Column<int>(type: "INTEGER", nullable: false),
                    DecksUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    DecksUsedToday = table.Column<int>(type: "INTEGER", nullable: false),
                    BoatAttacks = table.Column<int>(type: "INTEGER", nullable: false),
                    RepairPoints = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerWarSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerWarSnapshots_WarSnapshots_WarSnapshotId",
                        column: x => x.WarSnapshotId,
                        principalTable: "WarSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWarSnapshots_PlayerTag",
                table: "PlayerWarSnapshots",
                column: "PlayerTag");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWarSnapshots_WarSnapshotId_PlayerTag",
                table: "PlayerWarSnapshots",
                columns: new[] { "WarSnapshotId", "PlayerTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarSnapshots_ClanId_SeasonId_SectionIndex_PeriodIndex",
                table: "WarSnapshots",
                columns: new[] { "ClanId", "SeasonId", "SectionIndex", "PeriodIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerWarSnapshots");

            migrationBuilder.DropTable(
                name: "WarSnapshots");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAtUtc",
                table: "Clans");

            migrationBuilder.DropColumn(
                name: "PlanTier",
                table: "Clans");
        }
    }
}
