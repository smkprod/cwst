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
    IClashRoyaleApi crApi,
    GetPlayerStatsUseCase getStats,
    GetGlobalTopUseCase getGlobalTop,
    GetPlayerTournamentHistoryUseCase getTournamentHistory) : ControllerBase
{
    /// <summary>
    /// GET /api/players/top — глобальный топ игроков, привязавших аккаунт к боту,
    /// по всем кланам сервиса (слава за последние недели).
    /// </summary>
    [HttpGet("top")]
    public async Task<IActionResult> GlobalTop(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return Ok(await getGlobalTop.ExecuteAsync(userId, ct));
    }

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
    /// GET /api/players/{tag}/history — история войн игрока по неделям (tag без #).
    /// Источники: собственные снапшоты сервиса + официальный журнал войн его
    /// текущего клана (/riverracelog, до 10 недель). Полная история — на RoyaleAPI.
    /// </summary>
    [HttpGet("{tag}/history")]
    public async Task<IActionResult> History(string tag, [FromQuery] int weeks, CancellationToken ct)
    {
        var playerTag = "#" + tag.TrimStart('#').ToUpperInvariant();
        var maxWeeks = weeks is > 0 and <= 26 ? weeks : 12;
        var rows = await snapshots.GetPlayerHistoryAsync(playerTag, maxWeeks, ct);

        var merged = rows.Select(r => new PlayerWeekHistoryDto(
            SeasonId: r.Snapshot!.SeasonId,
            SectionIndex: r.Snapshot.SectionIndex,
            IsColosseum: r.Snapshot.PeriodType == "colosseum",
            ClanTag: r.Snapshot.Clan?.ClanTag ?? "",
            ClanName: r.Snapshot.Clan?.Name ?? "—",
            Fame: r.Fame,
            DecksUsed: r.DecksUsed,
            AvgFamePerAttack: r.Fame > 0 && r.DecksUsed > 0
                ? Math.Round(Math.Clamp((double)r.Fame / r.DecksUsed, 100, 250), 1)
                : 0)).ToList();

        // Дополняем недостающие недели официальным журналом текущего клана игрока
        try
        {
            var clanTag = await crApi.GetPlayerClanTagAsync(playerTag, ct);
            if (clanTag is not null)
            {
                var seen = merged.Select(m => (m.SeasonId, m.SectionIndex)).ToHashSet();
                var log = await crApi.GetRiverRaceLogAsync(clanTag, ct);
                foreach (var w in log)
                {
                    if (seen.Contains((w.SeasonId, w.SectionIndex))) continue;
                    var standing = w.Standings.FirstOrDefault(s =>
                        string.Equals(s.ClanTag, clanTag, StringComparison.OrdinalIgnoreCase));
                    var me = standing?.Participants.FirstOrDefault(p =>
                        string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
                    if (me is null || me.Fame == 0) continue;

                    merged.Add(new PlayerWeekHistoryDto(
                        SeasonId: w.SeasonId,
                        SectionIndex: w.SectionIndex,
                        IsColosseum: w.IsColosseum,
                        ClanTag: standing!.ClanTag,
                        ClanName: standing.ClanName,
                        Fame: me.Fame,
                        DecksUsed: me.DecksUsed,
                        AvgFamePerAttack: me.Fame > 0 && me.DecksUsed > 0
                            ? Math.Round(Math.Clamp((double)me.Fame / me.DecksUsed, 100, 250), 1)
                            : 0));
                }
            }
        }
        catch { /* журнал недоступен — показываем только свои данные */ }

        var dto = new PlayerHistoryDto(
            PlayerTag: playerTag,
            RoyaleApiUrl: $"https://royaleapi.com/player/{Uri.EscapeDataString(playerTag.TrimStart('#'))}",
            Weeks: merged
                .OrderByDescending(m => m.SeasonId).ThenByDescending(m => m.SectionIndex)
                .Take(maxWeeks)
                .ToList());

        return Ok(dto);
    }

    /// <summary>GET /api/players/{tag}/tournaments — история участия игрока в турнирах Clanify.</summary>
    [HttpGet("{tag}/tournaments")]
    public async Task<IActionResult> Tournaments(string tag, CancellationToken ct)
    {
        var playerTag = "#" + tag.TrimStart('#').ToUpperInvariant();
        return Ok(await getTournamentHistory.ExecuteAsync(playerTag, ct));
    }

    /// <summary>
    /// GET /api/players/{tag}/profile — полный профиль игрока: уровень, трофеи, клан,
    /// карты и агрегированная статистика войн из официального журнала.
    /// </summary>
    [HttpGet("{tag}/profile")]
    public async Task<IActionResult> Profile(string tag, CancellationToken ct)
    {
        var playerTag = "#" + tag.TrimStart('#').ToUpperInvariant();

        var info = await crApi.GetPlayerInfoAsync(playerTag, ct);
        if (info is null) return NotFound(new { error = "player_not_found", message = "Игрок не найден" });

        // Статистика войн из журнала текущего клана
        var weeksPlayed = 0;
        var totalFame = 0;
        var totalDecks = 0;

        try
        {
            if (info.ClanTag is not null)
            {
                var log = await crApi.GetRiverRaceLogAsync(info.ClanTag, ct);
                foreach (var week in log)
                {
                    foreach (var standing in week.Standings)
                    {
                        var me = standing.Participants.FirstOrDefault(p =>
                            string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
                        if (me is null || me.Fame == 0) continue;
                        weeksPlayed++;
                        totalFame += me.Fame;
                        totalDecks += me.DecksUsed;
                    }
                }
            }
        }
        catch { /* журнал недоступен */ }

        var avgFame = totalDecks > 0
            ? Math.Round((double)totalFame / totalDecks, 1)
            : 0;

        var profileDto = new PlayerProfileDto(
            PlayerTag: info.Tag,
            Name: info.Name,
            ExpLevel: info.ExpLevel,
            Trophies: info.Trophies,
            ClanWarTrophies: info.ClanWarTrophies,
            ClanName: info.ClanName,
            ClanTag: info.ClanTag,
            ArenaName: info.ArenaName,
            Cards: info.Cards.Select(c => new PlayerCardDto(c.Name, c.Level, c.MaxLevel, c.IconUrl)).ToList(),
            WeeksPlayed: weeksPlayed,
            TotalFame: totalFame,
            AvgFamePerAttack: avgFame,
            RoyaleApiUrl: $"https://royaleapi.com/player/{Uri.EscapeDataString(playerTag.TrimStart('#'))}");

        return Ok(profileDto);
    }
}
