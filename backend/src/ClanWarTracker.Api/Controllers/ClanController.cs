using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/clans")]
public class ClanController(
    GetClanStatusUseCase getStatus,
    GetClanHistoryUseCase getHistory,
    GetSeasonStatsUseCase getSeason,
    NudgePlayersUseCase nudge,
    IPlayerRepository players,
    IClanRepository clans,
    IClashRoyaleApi crApi,
    ITelegramBotClient bot,
    IMemoryCache cache,
    IConfiguration config) : ControllerBase
{
    /// <summary>GET /api/clans/my/status — статус войны клана текущего пользователя.</summary>
    [HttpGet("my/status")]
    public async Task<IActionResult> GetMyClanStatus(CancellationToken ct)
    {
        var (player, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        var status = await getStatus.ExecuteAsync(clan!.ClanTag, ct);
        if (status is null) return NotFound(new { error = "war_not_found" });

        // Подмешиваем контекст текущего пользователя
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var isAdmin = await IsClanAdminAsync(clan.TelegramChatId, userId, ct);

        // Роль в CR-клане (leader/coLeader → доступ к настройкам бота из Mini App)
        var crRole = await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player!.PlayerTag, ct);
        var isClanLeader = crRole is "leader" or "coLeader";

        return Ok(new { status.ClanTag, status.ClanName, status.PeriodType, status.PeriodIndex,
                        status.DayEndsAtUtc, status.HoursLeft, status.Plan, status.Stats, status.Forecast,
                        status.Race, status.Players, myPlayerTag = player.PlayerTag, isAdmin, isClanLeader,
                        isOwner = IsOwner(userId), reminderHoursBeforeEnd = clan.ReminderHoursBeforeEnd });
    }

    public record ReminderSettingsRequest(int HoursBeforeEnd);

    /// <summary>
    /// POST /api/clans/my/reminder — за сколько часов до конца военного дня слать
    /// автонапоминания не доигравшим 4/4 (только админ группы). Body: { hoursBeforeEnd: 1..12 }.
    /// </summary>
    [HttpPost("my/reminder")]
    public async Task<IActionResult> SetReminderHours([FromBody] ReminderSettingsRequest req, CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        if (!await IsClanAdminAsync(clan!.TelegramChatId, userId, ct))
            return StatusCode(403, new { error = "not_admin", message = "Менять время напоминаний может только админ группы" });

        if (req.HoursBeforeEnd is < 1 or > 12)
            return BadRequest(new { error = "bad_hours", message = "Часы до конца дня: от 1 до 12" });

        clan.ReminderHoursBeforeEnd = req.HoursBeforeEnd;
        await clans.SaveChangesAsync(ct);
        return Ok(new { ok = true, reminderHoursBeforeEnd = clan.ReminderHoursBeforeEnd });
    }

    /// <summary>GET /api/clans/{tag}/status — статус по тегу (tag без #, напр. ABC123).</summary>
    [HttpGet("{tag}/status")]
    public async Task<IActionResult> GetClanStatus(string tag, CancellationToken ct)
    {
        var status = await getStatus.ExecuteAsync("#" + tag.ToUpperInvariant(), ct);
        return status is null ? NotFound(new { error = "war_not_found" }) : Ok(status);
    }

    /// <summary>GET /api/clans/my/history?weeks=8 — история войн по неделям (Pro).</summary>
    [HttpGet("my/history")]
    public async Task<IActionResult> GetMyClanHistory([FromQuery] int weeks, CancellationToken ct)
    {
        var (player, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        if (clan!.EffectivePlan(DateTime.UtcNow) != PlanTier.Pro)
            return StatusCode(403, new { error = "pro_required", message = "История войн доступна на Pro" });

        var history = await getHistory.ExecuteAsync(
            clan.Id, weeks is > 0 and <= 26 ? weeks : 8, player!.PlayerTag, ct);
        return Ok(history);
    }

    /// <summary>GET /api/clans/my/season — сезонный зачёт: кто сколько набил за сезон (Pro).</summary>
    [HttpGet("my/season")]
    public async Task<IActionResult> GetMyClanSeason(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        if (clan!.EffectivePlan(DateTime.UtcNow) != PlanTier.Pro)
            return StatusCode(403, new { error = "pro_required", message = "Сезонный зачёт доступен на Pro" });

        var season = await getSeason.ExecuteAsync(clan.Id, null, ct);
        return season is null
            ? NotFound(new { error = "no_season_data", message = "Данные сезона ещё не накопились" })
            : Ok(season);
    }

    /// <summary>POST /api/clans/my/nudge — «пнуть» всех не сыгравших (Admin/Leader; Free: до 20 чел.).</summary>
    [HttpPost("my/nudge")]
    public async Task<IActionResult> NudgeSlackers(CancellationToken ct)
    {
        var (player, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var isAdmin = await IsClanAdminAsync(clan!.TelegramChatId, userId, ct);
        var crRole = await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player!.PlayerTag, ct);
        var isClanLeader = crRole is "leader" or "coLeader";

        if (!isAdmin && !isClanLeader)
            return StatusCode(403, new { error = "not_admin", message = "Пинать может только админ группы или лидер клана" });

        var isPro = clan.EffectivePlan(DateTime.UtcNow) == PlanTier.Pro;
        var result = await nudge.ExecuteAsync(clan.Id, isPro, ct);
        return result is null
            ? Conflict(new { error = "no_war_day", message = "Сейчас не день войны — пинать некого" })
            : Ok(result);
    }

    private async Task<(Player? Player, Clan? Clan, IActionResult? Error)> ResolvePlayerClanAsync(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null)
            return (null, null, NotFound(new { error = "player_not_linked", message = "Сначала привяжи тег: /link #ТЕГ" }));

        var clan = await clans.GetByIdAsync(player.ClanId, ct);

        // Авто-переключение: если игрок в CR сейчас в другом клане,
        // и этот клан зарегистрирован в сервисе — следуем за игроком.
        try
        {
            var actualTag = await crApi.GetPlayerClanTagAsync(player.PlayerTag, ct);
            if (actualTag is not null && !string.Equals(actualTag, clan?.ClanTag, StringComparison.OrdinalIgnoreCase))
            {
                var actualClan = await clans.GetByTagAsync(actualTag, ct);
                if (actualClan is not null)
                {
                    player.ClanId = actualClan.Id;
                    await players.SaveChangesAsync(ct);
                    clan = actualClan;
                }
            }
        }
        catch
        {
            // CR API недоступен — работаем по сохранённой привязке
        }

        return clan is null
            ? (player, null, NotFound(new { error = "clan_not_found" }))
            : (player, clan, null);
    }

    private bool IsOwner(long userId) =>
        long.TryParse(config["Owner:TelegramUserId"], out var ownerId) && ownerId != 0 && userId == ownerId;

    private async Task<bool> IsClanAdminAsync(long chatId, long userId, CancellationToken ct)
    {
        if (chatId == 0) return false;

        // Кэш 5 минут: /my/status опрашивается каждым открытым Mini App раз в минуту,
        // а GetChatMember — живой сетевой вызов к Telegram (сотни мс на каждый запрос)
        return await cache.GetOrCreateAsync($"tgadmin:{chatId}:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            try
            {
                var member = await bot.GetChatMember(chatId, userId, ct);
                return member.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator;
            }
            catch
            {
                return false; // бот не в чате / чат удалён — не админ
            }
        });
    }
}
