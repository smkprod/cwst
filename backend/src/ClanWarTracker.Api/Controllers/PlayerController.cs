using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayerController(
    IPlayerRepository players,
    IClanRepository clans,
    IWarSnapshotRepository snapshots,
    IClashRoyaleApi crApi,
    GetPlayerStatsUseCase getStats,
    GetGlobalTopUseCase getGlobalTop,
    GetPlayerTournamentHistoryUseCase getTournamentHistory,
    GetAchievementsUseCase getAchievements,
    GetWhatsNewUseCase getWhatsNew,
    GiveRespectUseCase giveRespect,
    SuggestDecksUseCase suggestDecks,
    IRespectRepository respects) : ControllerBase
{
    /// <summary>
    /// GET /api/players/{tag}/decks — какие колоды меты игрок может собрать из своей
    /// коллекции и до каких не хватает пары карт.
    /// </summary>
    [HttpGet("{tag}/decks")]
    public async Task<IActionResult> Decks(string tag, CancellationToken ct)
    {
        var playerTag = "#" + tag.TrimStart('#').ToUpperInvariant();

        DeckSuggestionsDto? result;
        try { result = await suggestDecks.ExecuteAsync(playerTag, ct); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }

        return result is null
            ? NotFound(new { error = "player_not_found", message = "Игрок не найден" })
            : Ok(result);
    }

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
    /// GET /api/players/me/whats-new — персональная дельта с прошлого визита.
    /// ВАЖНО: вызов обновляет отметку визита, поэтому дёргается один раз при входе.
    /// </summary>
    [HttpGet("me/whats-new")]
    public async Task<IActionResult> WhatsNew(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var data = await getWhatsNew.ExecuteAsync(userId, ct);
        return data is null ? NoContent() : Ok(data);
    }

    /// <summary>POST /api/players/{tag}/respect — дать респект 👏 согильдийцу (1 в сутки).</summary>
    [HttpPost("{tag}/respect")]
    public async Task<IActionResult> GiveRespect(string tag, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var result = await giveRespect.ExecuteAsync(userId, tag, ct);
        return result.Ok
            ? Ok(new { ok = true, total = result.TargetTotal })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>GET /api/players/me/respect-status — дал ли я уже респект сегодня.</summary>
    [HttpGet("me/respect-status")]
    public async Task<IActionResult> RespectStatus(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null) return NotFound(new { error = "player_not_linked" });

        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var given = await respects.GetByGiverAndDayAsync(player.PlayerTag, day, ct);
        var (total, _) = await respects.CountForPlayerAsync(player.PlayerTag, DateTime.UtcNow, ct);
        return Ok(new { givenToday = given is not null, givenToName = given?.ToName, myTotal = total });
    }

    /// <summary>
    /// GET /api/players/me/achievements — витрина наград: значки с уровнями и
    /// прогрессом до следующего (считается из накопленных снапшотов клана).
    /// </summary>
    [HttpGet("me/achievements")]
    public async Task<IActionResult> MyAchievements(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null) return NotFound(new { error = "player_not_linked" });
        if (player.ClanId is not int clanId)
            return NotFound(new { error = "no_clan", message = "Игрок не состоит в клане бота" });

        return Ok(await getAchievements.ExecuteAsync(clanId, player.PlayerTag, ct));
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
                : 0,
            ClanAvgFamePerAttack: r.Snapshot.TotalFame > 0 && r.Snapshot.TotalDecksUsed > 0
                ? Math.Round(Math.Clamp((double)r.Snapshot.TotalFame / r.Snapshot.TotalDecksUsed, 100, 250), 1)
                : 0)).ToList();

        // Официальный журнал клана — ПРИОРИТЕТНЫЙ источник: он пофамильный и всегда полный,
        // тогда как наш снимок недели мог сняться неудачно (например, колизей с 0 славы) и
        // раньше такая «дырка» навсегда перекрывала правильные данные из API.
        // Поэтому: неделя из журнала перезаписывает нашу, а снимки остаются только для
        // недель, которых в журнале нет (он отдаёт ~10 недель и только по текущему клану).
        try
        {
            var clanTag = await crApi.GetPlayerClanTagAsync(playerTag, ct);
            if (clanTag is not null)
            {
                var log = await crApi.GetRiverRaceLogAsync(clanTag, ct);
                foreach (var w in log)
                {
                    var standing = w.Standings.FirstOrDefault(s =>
                        string.Equals(s.ClanTag, clanTag, StringComparison.OrdinalIgnoreCase));
                    var me = standing?.Participants.FirstOrDefault(p =>
                        string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
                    if (me is null || me.Fame == 0) continue; // не участвовал — не затираем

                    var clanDecks = standing!.Participants.Sum(p => p.DecksUsed);
                    var fromLog = new PlayerWeekHistoryDto(
                        SeasonId: w.SeasonId,
                        SectionIndex: w.SectionIndex,
                        IsColosseum: w.IsColosseum,
                        ClanTag: standing.ClanTag,
                        ClanName: standing.ClanName,
                        Fame: me.Fame,
                        DecksUsed: me.DecksUsed,
                        AvgFamePerAttack: me.Fame > 0 && me.DecksUsed > 0
                            ? Math.Round(Math.Clamp((double)me.Fame / me.DecksUsed, 100, 250), 1)
                            : 0,
                        ClanAvgFamePerAttack: standing.Fame > 0 && clanDecks > 0
                            ? Math.Round(Math.Clamp((double)standing.Fame / clanDecks, 100, 250), 1)
                            : 0);

                    var existing = merged.FindIndex(m =>
                        m.SeasonId == w.SeasonId && m.SectionIndex == w.SectionIndex);
                    if (existing >= 0) merged[existing] = fromLog;
                    else merged.Add(fromLog);
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

        static PathOfLegendDto? Pol(Domain.Entities.CrPathOfLegend? p) =>
            p is null ? null : new PathOfLegendDto(p.Trophies, p.LeagueNumber, p.Rank);

        // Разбор под набор — Pro-функция. Тариф смотрим у клана ТОГО, КТО СМОТРИТ:
        // это его инструмент подбора, а не свойство просматриваемого игрока.
        // Исключение — собственный профиль: свой разбор человек видит всегда, тарифом
        // закрыт подбор ЧУЖИХ игроков, а не право знать про себя.
        var viewerId = (long)HttpContext.Items["TelegramUserId"]!;
        var viewer = await players.GetByTelegramIdAsync(viewerId, ct);
        var viewerClan = viewer?.ClanId is int vc ? await clans.GetByIdAsync(vc, ct) : null;
        var viewerIsPro = viewerClan?.EffectivePlan(DateTime.UtcNow) == PlanTier.Pro;
        var isSelf = string.Equals(viewer?.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase);

        var analysis = viewerIsPro || isSelf
            ? AnalyzePlayerService.Build(info, weeksPlayed, avgFame)
            : null;

        var profileDto = new PlayerProfileDto(
            PlayerTag: info.Tag,
            Name: info.Name,
            ExpLevel: info.ExpLevel,
            Trophies: info.Trophies,
            BestTrophies: info.BestTrophies,
            ClanWarTrophies: info.ClanWarTrophies,
            ClanName: info.ClanName,
            ClanTag: info.ClanTag,
            ArenaName: info.ArenaName,
            Cards: info.Cards.Select(c => new PlayerCardDto(c.Name, c.Level, c.MaxLevel, c.IconUrl,
                c.EvolutionLevel, c.MaxEvolutionLevel, c.EvoIconUrl)).ToList(),
            WeeksPlayed: weeksPlayed,
            TotalFame: totalFame,
            AvgFamePerAttack: avgFame,
            WarDayWins: info.WarDayWins,
            BattleCount: info.BattleCount,
            ThreeCrownWins: info.ThreeCrownWins,
            CurrentWinLoseStreak: info.CurrentWinLoseStreak,
            CurrentPathOfLegend: Pol(info.CurrentPathOfLegend),
            BestPathOfLegend: Pol(info.BestPathOfLegend),
            CurrentFavouriteCard: info.CurrentFavouriteCard,
            CurrentDeck: info.CurrentDeck.Select(c => new PlayerCardDto(c.Name, c.Level, c.MaxLevel, c.IconUrl,
                c.EvolutionLevel, c.MaxEvolutionLevel, c.EvoIconUrl)).ToList(),
            Wins: info.Wins,
            Losses: info.Losses,
            MaxCardLevel: info.MaxCardLevel,
            Analysis: analysis,
            RoyaleApiUrl: $"https://royaleapi.com/player/{Uri.EscapeDataString(playerTag.TrimStart('#'))}");

        return Ok(profileDto);
    }
}
