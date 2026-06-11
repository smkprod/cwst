using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayerController(
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    GetPlayerStatsUseCase getStats) : ControllerBase
{
    /// <summary>GET /api/players/me — текущий привязанный игрок.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        return player is null
            ? NotFound(new { error = "player_not_linked" })
            : Ok(new { player.PlayerTag, player.Name });
    }

    /// <summary>GET /api/players/me/stats — детальная статистика по текущему игроку.</summary>
    [HttpGet("me/stats")]
    public async Task<IActionResult> MyStats(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var stats = await getStats.ExecuteAsync(userId, ct);
        if (stats is null)
        {
            // Различаем "не привязан" и "не нашли в текущей войне"
            var player = await players.GetByTelegramIdAsync(userId, ct);
            return player is null
                ? NotFound(new { error = "player_not_linked" })
                : NotFound(new { error = "not_in_current_war", message = "Игрока нет в текущем составе войны" });
        }
        return Ok(stats);
    }

    /// <summary>
    /// GET /api/players/{tag}/history — история войн игрока по данным сервиса:
    /// в каких кланах играл и сколько славы набивал по неделям (tag без #).
    /// Покрывает только кланы, подключённые к боту; полная история — на RoyaleAPI.
    /// </summary>
    [HttpGet("{tag}/history")]
    public async Task<IActionResult> History(string tag, [FromQuery] int weeks, CancellationToken ct)
    {
        var playerTag = "#" + tag.TrimStart('#').ToUpperInvariant();
        var rows = await snapshots.GetPlayerHistoryAsync(
            playerTag, weeks is > 0 and <= 26 ? weeks : 12, ct);

        var dto = new PlayerHistoryDto(
            PlayerTag: playerTag,
            RoyaleApiUrl: $"https://royaleapi.com/player/{Uri.EscapeDataString(playerTag.TrimStart('#'))}",
            Weeks: rows.Select(r => new PlayerWeekHistoryDto(
                SeasonId: r.Snapshot!.SeasonId,
                SectionIndex: r.Snapshot.SectionIndex,
                IsColosseum: r.Snapshot.PeriodType == "colosseum",
                ClanTag: r.Snapshot.Clan?.ClanTag ?? "",
                ClanName: r.Snapshot.Clan?.Name ?? "—",
                Fame: r.Fame,
                DecksUsed: r.DecksUsed,
                AvgFamePerAttack: r.Fame > 0 && r.DecksUsed > 0
                    ? Math.Round(Math.Clamp((double)r.Fame / r.DecksUsed, 100, 250), 1)
                    : 0)).ToList());

        return Ok(dto);
    }
}
