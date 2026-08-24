using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
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
    GetClanDisciplineUseCase getDiscipline,
    GetClanHistoryUseCase getHistory,
    GetSeasonStatsUseCase getSeason,
    GetSeasonBreakdownUseCase getSeasonBreakdown,
    GetSeasonArchiveUseCase getSeasonArchive,
    NudgePlayersUseCase nudge,
    IPlayerRepository players,
    IClanRepository clans,
    IWarBattleRepository warBattles,
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

        ClanStatusDto? status;
        try { status = await getStatus.ExecuteAsync(clan!.ClanTag, ct); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }
        if (status is null) return NotFound(new { error = "war_not_found" });

        // Подмешиваем контекст текущего пользователя
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var isAdmin = await IsClanAdminAsync(clan.TelegramChatId, userId, ct);

        // Роль в CR-клане (leader/coLeader → доступ к настройкам бота из Mini App)
        var crRole = await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player!.PlayerTag, ct);
        var isClanLeader = crRole is "leader" or "coLeader";

        return Ok(new { status.ClanTag, status.ClanName, status.PeriodType, status.PeriodIndex,
                        status.DayEndsAtUtc, status.HoursLeft, status.Plan, status.Stats, status.Forecast,
                        status.Race, status.Players, status.Insights, status.WarLog, status.DayLogs,
                        myPlayerTag = player.PlayerTag,
                        isAdmin, isClanLeader,
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

    public record NotificationSettingsDto(
        int ReminderHoursBeforeEnd,
        bool RemindersEnabled, string RemindersChannel,
        bool WarStartEnabled, string WarStartChannel,
        bool FinalCallEnabled,
        bool DailyReportEnabled,
        int? WarEndMinuteUtc = null,    // во сколько заканчивается КВ (минуты от 00:00 UTC), null = 10:00 по умолчанию
        bool PerfectDayEnabled = true,  // поздравление «900 за день» в чат
        string Language = "ru");        // язык сообщений бота: ru | uk | en

    /// <summary>GET /api/clans/my/notification-settings — гибкие настройки уведомлений (админ/лидер).</summary>
    [HttpGet("my/notification-settings")]
    public async Task<IActionResult> GetNotificationSettings(CancellationToken ct)
    {
        var (player, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;
        if (!await CanManageAsync(clan!, player!, ct))
            return StatusCode(403, new { error = "not_admin", message = "Настройки доступны админу группы или лидеру клана" });

        return Ok(ToDto(NotificationSettings.Parse(clan!.NotificationSettingsJson), clan.ReminderHoursBeforeEnd));
    }

    /// <summary>POST /api/clans/my/notification-settings — сохранить настройки уведомлений.</summary>
    [HttpPost("my/notification-settings")]
    public async Task<IActionResult> SetNotificationSettings([FromBody] NotificationSettingsDto dto, CancellationToken ct)
    {
        var (player, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;
        if (!await CanManageAsync(clan!, player!, ct))
            return StatusCode(403, new { error = "not_admin", message = "Настройки доступны админу группы или лидеру клана" });

        if (dto.ReminderHoursBeforeEnd is < 1 or > 12)
            return BadRequest(new { error = "bad_hours", message = "Часы до конца дня: от 1 до 12" });

        if (dto.WarEndMinuteUtc is int end && (end < 0 || end >= 1440))
            return BadRequest(new { error = "bad_time", message = "Время конца дня: 0..1439 минут UTC" });

        clan!.ReminderHoursBeforeEnd = dto.ReminderHoursBeforeEnd;
        clan.NotificationSettingsJson = new NotificationSettings
        {
            Reminders = new ToggleChannel { Enabled = dto.RemindersEnabled, Channel = NotifyChannelExt.ParseChannel(dto.RemindersChannel) },
            WarStart = new ToggleChannel { Enabled = dto.WarStartEnabled, Channel = NotifyChannelExt.ParseChannel(dto.WarStartChannel) },
            FinalCall = new Toggle { Enabled = dto.FinalCallEnabled },
            DailyReport = new Toggle { Enabled = dto.DailyReportEnabled },
            PerfectDay = new Toggle { Enabled = dto.PerfectDayEnabled },
            WarEndMinuteUtc = dto.WarEndMinuteUtc,
            // Нормализуем через разбор: незнакомый код языка станет русским, а не осядет
            // в базе мусором, который потом придётся чистить.
            Language = BotText.ToWire(BotText.ParseLang(dto.Language)),
        }.Serialize();
        await clans.SaveChangesAsync(ct);

        return Ok(ToDto(NotificationSettings.Parse(clan.NotificationSettingsJson), clan.ReminderHoursBeforeEnd));
    }

    private static NotificationSettingsDto ToDto(NotificationSettings s, int hours) => new(
        hours,
        s.Reminders.Enabled, s.Reminders.Channel.ToWire(),
        s.WarStart.Enabled, s.WarStart.Channel.ToWire(),
        s.FinalCall.Enabled,
        s.DailyReport.Enabled,
        s.WarEndMinuteUtc,
        s.PerfectDay.Enabled,
        BotText.ToWire(s.Lang));

    /// <summary>Управлять настройками может админ группы или лидер/со-лидер клана.</summary>
    private async Task<bool> CanManageAsync(Clan clan, Player player, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        if (await IsClanAdminAsync(clan.TelegramChatId, userId, ct)) return true;
        try
        {
            var role = await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player.PlayerTag, ct);
            return role is "leader" or "coLeader";
        }
        catch { return false; }
    }

    /// <summary>GET /api/clans/{tag}/status — статус по тегу (tag без #, напр. ABC123).</summary>
    [HttpGet("{tag}/status")]
    public async Task<IActionResult> GetClanStatus(string tag, CancellationToken ct)
    {
        ClanStatusDto? status2;
        try { status2 = await getStatus.ExecuteAsync("#" + tag.ToUpperInvariant(), ct); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }
        return status2 is null ? NotFound(new { error = "war_not_found" }) : Ok(status2);
    }

    /// <summary>
    /// GET /api/clans/{tag}/overview — витрина любого клана по тегу: подключён ли он к боту,
    /// КВ-трофеи, состав и топ кланов его страны. Нужна игроку, чей клан ещё не с нами:
    /// он видит и своё положение, и куда можно перейти.
    /// </summary>
    [HttpGet("{tag}/overview")]
    public async Task<IActionResult> GetClanOverview(string tag, CancellationToken ct)
    {
        var clanTag = "#" + tag.TrimStart('#').ToUpperInvariant();
        var known = await clans.GetByTagAsync(clanTag, ct);

        CrClanInfo? info;
        ClanWarRanking? rank = null;
        List<CrClanMember> roster;
        try
        {
            info = await crApi.GetClanInfoAsync(clanTag, ct);
            if (info is null && known is null) return NotFound(new { error = "clan_not_found" });

            roster = await crApi.GetClanRosterAsync(clanTag, ct);
            // Рейтинг — приятное дополнение, а не условие показа карточки: он тянет
            // тяжёлые списки топ-1000 и может не ответить.
            try { rank = await crApi.GetClanWarRankingAsync(clanTag, ct); }
            catch { rank = null; }
        }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }

        // Строго по старшинству, внутри ранга — по кубкам: так сразу видно, кто главный
        // и кто тянет клан, а не алфавит.
        var members = roster
            .OrderBy(m => m.Role switch
            {
                "leader" => 0,
                "coLeader" => 1,
                "elder" => 2,
                _ => 3,
            })
            .ThenByDescending(m => m.Trophies)
            .Select(m => new ClanMemberDto(m.Tag, m.Name, m.Role, m.Trophies, m.Donations))
            .ToList();

        return Ok(new ClanOverviewDto(
            ClanTag: clanTag,
            ClanName: info?.Name ?? known?.Name,
            Connected: known is not null,
            WarTrophies: info?.ClanWarTrophies ?? rank?.ClanWarTrophies ?? 0,
            MemberCount: info?.MemberCount ?? (members.Count > 0 ? members.Count : null),
            CountryName: info?.LocationName ?? rank?.CountryName,
            CountryRank: rank?.CountryRank,
            GlobalRank: rank?.GlobalRank,
            CountryTop: rank?.CountryTop.Select(c => new RankedClanDto(
                c.Rank, c.PreviousRank, c.Name, c.WarTrophies, c.Members,
                string.Equals(c.Tag, clanTag, StringComparison.OrdinalIgnoreCase))).ToList() ?? [],
            Description: info?.Description,
            Type: info?.Type,
            ClanScore: info?.ClanScore ?? 0,
            RequiredTrophies: info?.RequiredTrophies ?? 0,
            DonationsPerWeek: info?.DonationsPerWeek ?? 0,
            Members: members));
    }

    /// <summary>
    /// GET /api/clans/{tag}/warlog — журнал прошлых войн любого клана из официального
    /// riverracelog (tag без #). IsOurClan помечает запрошенный клан.
    /// </summary>
    [HttpGet("{tag}/warlog")]
    public async Task<IActionResult> GetClanWarLog(string tag, CancellationToken ct)
    {
        var clanTag = "#" + tag.TrimStart('#').ToUpperInvariant();
        var log = await crApi.GetRiverRaceLogAsync(clanTag, ct);

        var weeks = log.Select(w => new WarLogWeekDto(
            SeasonId: w.SeasonId,
            SectionIndex: w.SectionIndex,
            IsColosseum: w.IsColosseum,
            Standings: w.Standings.Select(s => new WarLogClanDto(
                Rank: s.Rank,
                Name: s.ClanName,
                Fame: s.Fame,
                TrophyChange: s.TrophyChange,
                IsOurClan: string.Equals(s.ClanTag, clanTag, StringComparison.OrdinalIgnoreCase),
                Players: s.Participants
                    .OrderByDescending(p => p.Fame)
                    .Select(p => new WarLogPlayerDto(p.PlayerTag, p.Name, p.Fame, p.DecksUsed))
                    .ToList()))
                .ToList()))
            .ToList();

        return Ok(new { clanTag, weeks });
    }

    /// <summary>GET /api/clans/my/history?weeks=8 — история войн по неделям.</summary>
    [HttpGet("my/history")]
    public async Task<IActionResult> GetMyClanHistory([FromQuery] int weeks, CancellationToken ct)
    {
        var (player, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        var history = await getHistory.ExecuteAsync(
            clan!.Id, weeks is > 0 and <= 26 ? weeks : 8, player!.PlayerTag, ct);
        return Ok(history);
    }

    /// <summary>GET /api/clans/my/season — сезонный зачёт: кто сколько набил за сезон.</summary>
    [HttpGet("my/season")]
    public async Task<IActionResult> GetMyClanSeason(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        var season = await getSeason.ExecuteAsync(clan!.Id, null, ct);
        return season is null
            ? NotFound(new { error = "no_season_data", message = "Данные сезона ещё не накопились" })
            : Ok(season);
    }

    /// <summary>
    /// GET /api/clans/my/season-weeks — разбивка сезона по неделям (Война 1, Война 2 …)
    /// + общий зачёт за сезон. Прошлые недели берутся из официального журнала.
    /// </summary>
    [HttpGet("my/season-weeks")]
    public async Task<IActionResult> GetMyClanSeasonWeeks(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        var data = await getSeasonBreakdown.ExecuteAsync(clan!.ClanTag, ct);
        return data is null
            ? NotFound(new { error = "no_season_data", message = "Данные сезона ещё не накопились" })
            : Ok(data);
    }

    /// <summary>
    /// GET /api/clans/my/season-archive — архив лучших игроков за ПРОШЛЫЕ сезоны
    /// (текущий сезон живёт во вкладке «Сезон»). Пусто, пока не завершится первый сезон.
    /// </summary>
    [HttpGet("my/season-archive")]
    public async Task<IActionResult> GetMyClanSeasonArchive(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        return Ok(await getSeasonArchive.ExecuteAsync(clan!.Id, ct));
    }

    /// <summary>
    /// GET /api/clans/my/war-journal — журнал военных боёв клана за текущую неделю:
    /// кто/когда отыграл КВ и исход, + счёт побед/поражений.
    /// </summary>
    [HttpGet("my/war-journal")]
    public async Task<IActionResult> GetWarJournal(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        // Текущая неделя (сезон+секция) — из текущей гонки.
        WarStatus? war = null;
        try { war = await crApi.GetCurrentWarAsync(clan!.ClanTag, ct); }
        catch { /* API лежит — покажем пусто */ }
        if (war is null) return Ok(new WarJournalDto(0, 0, 0, []));

        var battles = await warBattles.GetByWeekAsync(clan!.Id, war.SeasonId, war.SectionIndex, ct);
        // Отсекаем бои раньше начала военной недели: раньше первый сбор мог затянуть старую
        // историю из боевого лога и пометить её текущей неделей. Только в военные дни — иначе
        // (в тренировочные дни) окно недели ещё не наступило и отфильтровало бы всё.
        if (war.IsWarDay)
            battles = battles.Where(b => b.BattleTimeUtc >= war.WarWeekStartUtc).ToList();
        var won = battles.Count(b => b.Won);

        return Ok(new WarJournalDto(
            Won: won,
            Lost: battles.Count - won,
            Total: battles.Count,
            Battles: battles
                .Select(b => new WarBattleDto(b.PlayerName, b.PlayerTag, b.BattleTimeUtc, b.Won, b.CrownsFor, b.CrownsAgainst))
                .ToList()));
    }

    /// <summary>
    /// GET /api/clans/my/discipline — кто недоигрывает войну, кого приходится пинать
    /// и кто тянет до последнего часа. Заменила «шанс победы»: тем числом глава ничего
    /// сделать не мог, а здесь каждая строка — конкретный человек и конкретный разговор.
    /// </summary>
    [HttpGet("my/discipline")]
    public async Task<IActionResult> GetDiscipline(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        try { return Ok(await getDiscipline.ExecuteAsync(clan!, ct)); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }
    }

    /// <summary>
    /// GET /api/clans/my/ranking — место клана в стране и мире по КВ-трофеям
    /// (официальные rankings из CR API; ранги только у топ-1000).
    /// </summary>
    [HttpGet("my/ranking")]
    public async Task<IActionResult> GetClanRanking(CancellationToken ct)
    {
        var (_, clan, error) = await ResolvePlayerClanAsync(ct);
        if (error is not null) return error;

        ClanWarRanking? rank;
        try { rank = await crApi.GetClanWarRankingAsync(clan!.ClanTag, ct); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }
        if (rank is null) return NotFound(new { error = "no_ranking" });

        return Ok(new ClanRankingDto(
            rank.ClanWarTrophies,
            rank.CountryName,
            rank.CountryRank,
            rank.CountryPreviousRank is > 0 ? rank.CountryPreviousRank : null,
            rank.GlobalRank,
            rank.GlobalPreviousRank is > 0 ? rank.GlobalPreviousRank : null,
            rank.CountryTop.Select(c => new RankedClanDto(
                c.Rank, c.PreviousRank, c.Name, c.WarTrophies, c.Members,
                string.Equals(c.Tag, clan!.ClanTag, StringComparison.OrdinalIgnoreCase))).ToList()));
    }

    /// <summary>POST /api/clans/my/nudge — «пнуть» всех не сыгравших (Admin/Leader; Free: до 5 чел.).</summary>
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
        NudgePlayersUseCase.NudgeResult? result;
        try { result = await nudge.ExecuteAsync(clan.Id, isPro, ct); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }
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

        var clan = player.ClanId.HasValue ? await clans.GetByIdAsync(player.ClanId.Value, ct) : null;

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
            entry.Size = 1;
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
