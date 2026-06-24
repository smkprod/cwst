using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanWarTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PrizeInfo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ClanInviteLink = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatorTelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatorPlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatorName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BestOf = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxParticipants = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BracketGeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TournamentParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TournamentId = table.Column<int>(type: "INTEGER", nullable: false),
                    TelegramUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerTag = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Seed = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FinalPlacement = table.Column<int>(type: "INTEGER", nullable: true),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentParticipants_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TournamentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantAId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParticipantBId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScoreA = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreB = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerParticipantId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    NextMatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    NextMatchSlot = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentParticipants_ParticipantAId",
                        column: x => x.ParticipantAId,
                        principalTable: "TournamentParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentParticipants_ParticipantBId",
                        column: x => x.ParticipantBId,
                        principalTable: "TournamentParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentParticipants_WinnerParticipantId",
                        column: x => x.WinnerParticipantId,
                        principalTable: "TournamentParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentMatches_NextMatchId",
                        column: x => x.NextMatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatorTelegramUserId",
                table: "Tournaments",
                column: "CreatorTelegramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentParticipants_TournamentId_PlayerTag",
                table: "TournamentParticipants",
                columns: new[] { "TournamentId", "PlayerTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentParticipants_TournamentId_TelegramUserId",
                table: "TournamentParticipants",
                columns: new[] { "TournamentId", "TelegramUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentId_Round_SlotIndex",
                table: "TournamentMatches",
                columns: new[] { "TournamentId", "Round", "SlotIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_ParticipantAId",
                table: "TournamentMatches",
                column: "ParticipantAId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_ParticipantBId",
                table: "TournamentMatches",
                column: "ParticipantBId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_WinnerParticipantId",
                table: "TournamentMatches",
                column: "WinnerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_NextMatchId",
                table: "TournamentMatches",
                column: "NextMatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentMatches");

            migrationBuilder.DropTable(
                name: "TournamentParticipants");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
