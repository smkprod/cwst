using System.Collections.Concurrent;
using System.Globalization;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Задания дня: три задания на местные сутки - два в игре и одно в приложении. Закрыл
/// все три - день идёт в серию 🔥, за серию дают дни Плюса.
///
/// Зачем: человек заходит, когда есть повод, а повод у бота был только у войны - с
/// четверга по воскресенье. Задания дают его каждый день, включая вторник и среду.
///
/// Задания в игре проверяются по журналу боёв на каждый запрос, как билеты челленджа:
/// подделать нечем. Задание в приложении (тренажёр, фишка, подбор колоды) отмечает
/// само приложение - ради него и открывают бота. В военные дни одно задание в игре -
/// бои КВ: задания тянут в ту же сторону, что и клан.
/// </summary>
public class DailyQuestsUseCase(
    IQuestRepository quests,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IPlayerBattleRepository battles,
    CollectPlayerBattlesUseCase collect,
    IClashRoyaleApi crApi,
    IClanRepository clans,
    PlusAccess plus,
    ISentNotificationRepository sentLog,
    INotificationSender sender,
    IServiceSettingRepository settings)
{
    public const string War = "war2";
    public const string Win = "win2";
    public const string Play = "play5";
    public const string Crowns = "crowns3";
    public const string Flawless = "flawless";
    public const string Streak = "streak2";
    public const string Trainer = "trainer";
    public const string Tip = "tip";
    public const string Decks = "decks";

    /// <summary>Попроще: их закрывает обычный вечер игры.</summary>
    private static readonly string[] Easy = [Win, Play];

    /// <summary>Посложнее: нужен хороший бой, а не просто бои.</summary>
    private static readonly string[] Hard = [Crowns, Flawless, Streak];

    /// <summary>В приложении. Тренажёр чаще: это то, ради чего стоит зайти, остальное - для разнообразия.</summary>
    private static readonly string[] Apps = [Trainer, Tip, Trainer, Decks, Trainer, Tip, Trainer];

    public static readonly string[] AppKeys = [Trainer, Tip, Decks];

    public static bool IsApp(string key) => AppKeys.Contains(key);

    public static int Target(string key) => key switch
    {
        War or Win or Streak => 2,
        Play => 5,
        _ => 1,
    };

    /// <summary>Рубильник владельца: «off» - вечерних напоминаний о серии нет.</summary>
    public const string RemindKillSwitchKey = "quests.remind";

    private const string RewardKind = "questreward";
    private const string RemindKind = "questremind";

    /// <summary>
    /// Больше стольких дней Плюса за серии за 30 дней не даём. Плюс здесь - попробовать,
    /// а не получать бесплатно: самые активные - те же люди, что его покупают, и без
    /// потолка идеальная серия давала бы им 13 дней из 30. И рвать серию ради новых
    /// «третьих дней» потолок тоже не даёт.
    /// </summary>
    public const int PlusCapPer30 = 3;

    /// <summary>Серия, за которую дают Плюс: третий день - день Плюса, каждый седьмой - два.</summary>
    public static int RewardFor(int streak) => streak <= 0 ? 0 : streak % 7 == 0 ? 2 : streak == 3 ? 1 : 0;

    /// <summary>Награда с учётом потолка: <paramref name="got30"/> - сколько уже дали за 30 дней.</summary>
    public static int Capped(int reward, int got30) => Math.Max(0, Math.Min(reward, PlusCapPer30 - got30));

    /// <summary>Через сколько закрытых дней следующая награда и какая - от текущей серии.</summary>
    public static (int In, int Days) NextReward(int streak)
    {
        for (var s = streak + 1; ; s++)
            if (RewardFor(s) is > 0 and var d) return (s - streak, d);
    }

    /// <summary>Сколько дней Плюса за серии человек получил за 30 дней, считая день <paramref name="day"/>.</summary>
    private async Task<int> Got30Async(long tg, string day, CancellationToken ct)
    {
        var from = DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(-29)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (await quests.GetForUserSinceAsync(tg, from, ct)).Sum(d => d.RewardDays);
    }

    /// <summary>Местная дата, yyyy-MM-dd.</summary>
    public static string DayOf(DateTime utc, int tzOffsetMinutes) =>
        utc.AddMinutes(tzOffsetMinutes).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Границы местного дня в UTC.</summary>
    public static (DateTime From, DateTime To) Window(string day, int tzOffsetMinutes)
    {
        var date = DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var from = DateTime.SpecifyKind(date.AddMinutes(-tzOffsetMinutes), DateTimeKind.Utc);
        return (from, from.AddDays(1));
    }

    /// <summary>Военные дни - с четверга по воскресенье: в эти дни одно задание - бои КВ.</summary>
    public static bool IsWarDay(string day) =>
        DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture).DayOfWeek
            is DayOfWeek.Thursday or DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// Задания на день. Одни и те же для человека и дня - а у соседей по клану разные:
    /// в чате интереснее сравнивать, кому что выпало.
    /// </summary>
    public static string[] Pick(string day, long telegramUserId, bool inClan)
    {
        var h = Fnv($"{day}|{telegramUserId}");
        var easy = Easy[(int)(h % (uint)Easy.Length)];
        var hard = Hard[(int)(h / 7 % (uint)Hard.Length)];
        var app = Apps[(int)(h / 49 % (uint)Apps.Length)];
        return inClan && IsWarDay(day) ? [War, easy, app] : [easy, hard, app];
    }

    /// <summary>Свой хэш вместо string.GetHashCode: тот в .NET меняется от запуска к запуску.</summary>
    private static uint Fnv(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h;
    }

    /// <summary>Сколько сделано по заданию в игре за день. Бои - по времени, по возрастанию.</summary>
    public static int Count(string key, IReadOnlyList<PlayerBattle> own, IReadOnlyList<CrBattle> war) => key switch
    {
        Win => own.Count(b => b.Result > 0 && MatchReport.ModeKey(b.Type) is "ladder" or "pol"),
        Play => own.Count,
        Crowns => own.Any(b => b.Result > 0 && b.CrownsFor >= 3) ? 1 : 0,
        // Соперник не взял ни одной короны - ни одна твоя башня не упала
        Flawless => own.Any(b => b.Result > 0 && b.CrownsAgainst == 0) ? 1 : 0,
        Streak => BestRun(own),
        War => war.Count,
        _ => 0,
    };

    private static int BestRun(IEnumerable<PlayerBattle> own)
    {
        int run = 0, best = 0;
        foreach (var b in own)
        {
            run = b.Result > 0 ? run + 1 : 0;
            best = Math.Max(best, run);
        }
        return best;
    }

    public static bool AllDone(QuestDay row) => row.DoneMask == (1 << row.Quests.Split(',').Length) - 1;

    /// <summary>Задание в приложении закрыто (или его нет) - только такой прошедший день ещё можно дозакрыть.</summary>
    public static bool AppDone(QuestDay row)
    {
        var keys = row.Quests.Split(',');
        for (var i = 0; i < keys.Length; i++)
            if (IsApp(keys[i]) && (row.DoneMask & (1 << i)) == 0) return false;
        return true;
    }

    public record ItemDto(string Key, string Kind, int Progress, int Target, bool Done);

    public record MateDto(string Name, int Streak, bool DoneToday, bool Me);

    /// <param name="DoneToday">Сколько человек клана закрыли сегодняшние задания.</param>
    /// <param name="Started">Сколько сегодня хотя бы открыли задания.</param>
    public record ClanDto(int DoneToday, int Started, List<MateDto> Top);

    /// <param name="Streak">Серия сейчас: с сегодняшним днём, если он закрыт, иначе до вчера.</param>
    /// <param name="AtRisk">Серия есть, а сегодня ещё не закрыто - сгорит в полночь.</param>
    /// <param name="NextRewardIn">Через сколько закрытых дней следующая награда.</param>
    /// <param name="RewardDays">Дней Плюса, выданных за сегодняшний день.</param>
    /// <param name="PlusCap">Потолок дней Плюса за серии на 30 дней. NextRewardDays 0 - выбран.</param>
    /// <param name="JustCompleted">День закрылся этим запросом - приложению праздновать.</param>
    public record QuestsDto(
        string Day, DateTime ResetsUtc, List<ItemDto> Items, bool Completed, int Streak, bool AtRisk, int BestStreak,
        int NextRewardIn, int NextRewardDays, int RewardDays, int PlusCap, bool JustCompleted, bool RemindOn, ClanDto? Clan);

    /// <summary>Задания на сегодня с проверкой по свежему журналу. null - аккаунт не привязан.</summary>
    public async Task<QuestsDto?> GetAsync(long tg, CancellationToken ct = default) => await RunAsync(tg, null, ct);

    /// <summary>Приложение отмечает своё задание: решил задачу в тренажёре, открыл фишку, подобрал колоду.</summary>
    public async Task<QuestsDto?> PingAsync(long tg, string kind, CancellationToken ct = default) =>
        IsApp(kind) ? await RunAsync(tg, kind, ct) : await RunAsync(tg, null, ct);

    public async Task SetRemindAsync(long tg, bool on, CancellationToken ct = default)
    {
        var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
        prefs.QuestRemindOff = !on;
        await alertPrefs.SaveChangesAsync(ct);
    }

    private async Task<QuestsDto?> RunAsync(long tg, string? appKind, CancellationToken ct)
    {
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return null;
        var prefs = await alertPrefs.GetAsync(tg, ct);
        var tz = prefs?.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
        var now = DateTime.UtcNow;
        var today = DayOf(now, tz);
        var yesterday = DayOf(now.AddDays(-1), tz);

        var row = await TodayRowAsync(tg, player.PlayerTag, player.ClanId, today, ct);
        if (row.CompletedUtc is null && (row.PlayerTag != player.PlayerTag || row.ClanId != player.ClanId))
        {
            // Перепривязал аккаунт или сменил клан - задания те же, считаем по новому
            row.PlayerTag = player.PlayerTag;
            row.ClanId = player.ClanId;
        }
        if (appKind is not null) Mark(row, appKind);

        try { await collect.SyncFreshAsync(player.PlayerTag, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { /* API игры недоступен - считаем по накопленному */ }

        // Сначала вчера: бои после последнего захода могли закрыть его уже за полночь,
        // и от этого зависит серия сегодня
        var history = await quests.GetForUserSinceAsync(tg, DayOf(now.AddDays(-60), tz), ct);
        var prev = await quests.GetAsync(tg, yesterday, ct);
        if (prev is { CompletedUtc: null } && AppDone(prev))
        {
            await EvaluateAsync(prev, tz, ct);
            await CompleteIfDoneAsync(prev, history.FirstOrDefault(d => d.Day == DayOf(now.AddDays(-2), tz)), ct);
        }

        var items = await EvaluateAsync(row, tz, ct);
        var just = await CompleteIfDoneAsync(row, prev, ct);
        await quests.SaveChangesAsync(ct);

        var completed = row.CompletedUtc is not null;
        var streak = completed ? row.Streak : prev?.CompletedUtc is not null ? prev.Streak : 0;
        var best = Math.Max(streak, history.Select(d => d.Streak).DefaultIfEmpty(0).Max());
        var (nextIn, nextDays) = NextReward(completed ? row.Streak : streak);
        // Потолок уже выбран - не обещаем того, чего не дадим (окно сдвинется - снова пообещаем)
        nextDays = Capped(nextDays, await Got30Async(tg, today, ct));
        var clan = player.ClanId is int clanId ? await ClanAsync(clanId, tg, today, yesterday, ct) : null;

        return new QuestsDto(today, Window(today, tz).To, items, completed, streak, !completed && streak > 0, best,
            nextIn, nextDays, row.RewardDays, PlusCapPer30, just, prefs?.QuestRemindOff != true, clan);
    }

    private async Task<QuestDay> TodayRowAsync(long tg, string tag, int? clanId, string today, CancellationToken ct) =>
        await quests.GetAsync(tg, today, ct)
        ?? await quests.AddAsync(new QuestDay
        {
            TelegramUserId = tg,
            PlayerTag = tag,
            ClanId = clanId,
            Day = today,
            Quests = string.Join(",", Pick(today, tg, clanId is not null)),
            CreatedUtc = DateTime.UtcNow,
        }, ct);

    private static void Mark(QuestDay row, string key)
    {
        var i = Array.IndexOf(row.Quests.Split(','), key);
        if (i >= 0) row.DoneMask |= 1 << i;
    }

    /// <summary>
    /// Прогресс по заданиям дня. Отметка «сделано» только добавляется: бой ушёл из
    /// журнала или кончился военный день - сделанное остаётся сделанным.
    /// </summary>
    private async Task<List<ItemDto>> EvaluateAsync(QuestDay row, int tz, CancellationToken ct)
    {
        var keys = row.Quests.Split(',');
        var (from, to) = Window(row.Day, tz);
        var pending = keys.Where((k, i) => !IsApp(k) && (row.DoneMask & (1 << i)) == 0).ToList();

        List<PlayerBattle> own = [];
        List<CrBattle> war = [];
        if (pending.Count > 0)
        {
            try
            {
                own = (await battles.GetSinceAsync(row.PlayerTag, from, ct))
                    .Where(b => b.BattleTimeUtc < to)
                    .OrderBy(b => b.BattleTimeUtc)
                    .ToList();
                if (pending.Contains(War))
                    war = (await crApi.GetPlayerBattlelogAsync(row.PlayerTag, ct))
                        .Where(b => b.BattleTimeUtc >= from && b.BattleTimeUtc < to)
                        .ToList();
            }
            catch (Exception) when (!ct.IsCancellationRequested) { /* без журнала покажем то, что уже отмечено */ }
        }

        var items = new List<ItemDto>();
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var target = Target(key);
            var marked = (row.DoneMask & (1 << i)) != 0;
            var progress = marked ? target : IsApp(key) ? 0 : Math.Min(target, Count(key, own, war));
            if (!marked && progress >= target) row.DoneMask |= 1 << i;
            items.Add(new ItemDto(key, IsApp(key) ? "app" : key == War ? "war" : "game", progress, target, progress >= target));
        }
        return items;
    }

    /// <summary>Все задания закрыты - день в серию и награда за неё. true - закрылся сейчас.</summary>
    private async Task<bool> CompleteIfDoneAsync(QuestDay row, QuestDay? prev, CancellationToken ct)
    {
        if (row.CompletedUtc is not null || !AllDone(row)) return false;
        row.CompletedUtc = DateTime.UtcNow;
        row.Streak = prev?.CompletedUtc is not null ? prev.Streak + 1 : 1;

        var reward = RewardFor(row.Streak);
        if (reward > 0) reward = Capped(reward, await Got30Async(row.TelegramUserId, row.Day, ct));
        // Отметка - до выдачи: два запроса разом закроют день оба, а Плюс выдаст один
        if (reward > 0 && await sentLog.TryClaimAsync(RewardKind, $"{row.TelegramUserId}:{row.Day}", ct))
        {
            try
            {
                await plus.GrantAsync(row.TelegramUserId, reward, Entitlement.Sources.Quest, row.PlayerTag, 0, null, ct);
                row.RewardDays = reward;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await sentLog.ReleaseAsync(RewardKind, $"{row.TelegramUserId}:{row.Day}", ct);
            }
        }
        await quests.SaveChangesAsync(ct);
        return true;
    }

    public const int ClanTopShown = 5;

    private async Task<ClanDto> ClanAsync(int clanId, long me, string today, string yesterday, CancellationToken ct)
    {
        var rows = await quests.GetForClanAsync(clanId, [today, yesterday], ct);
        var names = (await players.GetByClanIdAsync(clanId, ct))
            .Where(p => p.TelegramUserId is not null)
            .GroupBy(p => p.TelegramUserId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var mates = rows
            .GroupBy(r => r.TelegramUserId)
            .Where(g => names.ContainsKey(g.Key))
            .Select(g =>
            {
                var t = g.FirstOrDefault(r => r.Day == today);
                var y = g.FirstOrDefault(r => r.Day == yesterday);
                var done = t?.CompletedUtc is not null;
                var streak = done ? t!.Streak : y?.CompletedUtc is not null ? y.Streak : 0;
                return (Tg: g.Key, Started: t is not null, Done: done, Streak: streak);
            })
            .ToList();

        var top = mates
            .Where(m => m.Streak > 0)
            .OrderByDescending(m => m.Streak)
            .ThenByDescending(m => m.Done)
            .ThenBy(m => names[m.Tg], StringComparer.OrdinalIgnoreCase)
            .Take(ClanTopShown)
            .Select(m => new MateDto(names[m.Tg], m.Streak, m.Done, m.Tg == me))
            .ToList();
        return new ClanDto(mates.Count(m => m.Done), mates.Count(m => m.Started), top);
    }

    public record StatsDto(int StartedToday, int CompletedToday, int Streak3, int Streak7, int PlusDays30);

    /// <summary>Для панели владельца: день по Киеву, серии - у кого они живы сейчас.</summary>
    public static async Task<StatsDto> StatsAsync(IQuestRepository quests, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tz = TiltMessages.DefaultTzOffsetMinutes;
        var today = DayOf(now, tz);
        var yesterday = DayOf(now.AddDays(-1), tz);
        var rows = await quests.GetByDaysAsync([today, yesterday], ct);
        var streaks = rows
            .GroupBy(r => r.TelegramUserId)
            .Select(g =>
            {
                var t = g.FirstOrDefault(r => r.Day == today);
                var y = g.FirstOrDefault(r => r.Day == yesterday);
                return t?.CompletedUtc is not null ? t.Streak : y?.CompletedUtc is not null ? y.Streak : 0;
            })
            .ToList();
        var todays = rows.Where(r => r.Day == today).ToList();
        return new StatsDto(todays.Count, todays.Count(r => r.CompletedUtc is not null),
            streaks.Count(s => s >= 3), streaks.Count(s => s >= 7),
            await quests.RewardDaysSinceAsync(DayOf(now.AddDays(-30), tz), ct));
    }

    /// <summary>Когда день человека последний раз пересчитывали фоном - не чаще раза в полчаса.</summary>
    private static readonly ConcurrentDictionary<long, DateTime> LastEval = new();
    private static DateTime _lastPruneUtc = DateTime.MinValue;

    public const int RemindFromHourLocal = 19;
    public const int RemindToHourLocal = 22;
    public const int MaxRemindersPerTick = 40;

    /// <summary>
    /// Фоновый проход. Дозакрывает дни тех, кто сделал задание в приложении, а бои
    /// доиграл уже не открывая его, - серия не должна сгореть из-за того, что человек
    /// не зашёл посмотреть. И вечером, с 19 до 22 по местному, один раз пишет тем, у
    /// кого серия от двух дней и сегодня ещё не закрыто: сгорит в полночь. Это не
    /// рассылка: пишем только тем, кто сам ведёт серию, и выключается одной кнопкой.
    /// </summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var kv in LastEval) if (now - kv.Value > TimeSpan.FromHours(6)) LastEval.TryRemove(kv.Key, out _);
        if (now - _lastPruneUtc > TimeSpan.FromHours(12))
        {
            _lastPruneUtc = now;
            await quests.PruneAsync(DayOf(now.AddDays(-90), 0), ct);
        }

        var remindOn = !string.Equals(await settings.GetAsync(RemindKillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase);
        // Местные «сегодня» и «вчера» у всех часовых поясов попадают в эти четыре даты
        var days = Enumerable.Range(-2, 4).Select(d => DayOf(now.AddDays(d), 0)).ToList();
        var rows = (await quests.GetByDaysAsync(days, ct)).GroupBy(r => r.TelegramUserId).ToList();
        if (rows.Count == 0) return 0;
        var prefsByTg = (await alertPrefs.GetAllAsync(ct))
            .GroupBy(p => p.TelegramUserId)
            .ToDictionary(g => g.Key, g => g.First());

        var sent = 0;
        foreach (var user in rows)
        {
            if (ct.IsCancellationRequested) break;
            var tg = user.Key;
            var prefs = prefsByTg.GetValueOrDefault(tg);
            var tz = prefs?.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
            var local = now.AddMinutes(tz);
            var today = DayOf(now, tz);
            var y = user.FirstOrDefault(r => r.Day == DayOf(now.AddDays(-1), tz));
            var t = user.FirstOrDefault(r => r.Day == today);

            try
            {
                var due = now - LastEval.GetValueOrDefault(tg) >= TimeSpan.FromMinutes(30);
                // Вчера - только в первые часы после полуночи: бои вечера доезжают в журнал не сразу
                var finishYesterday = y is { CompletedUtc: null } && AppDone(y) && local.Hour < 6;
                var finishToday = t is { CompletedUtc: null } && AppDone(t);
                var atRisk = y is { CompletedUtc: not null, Streak: >= 2 } && t?.CompletedUtc is null;
                var remind = remindOn && atRisk && local.Hour is >= RemindFromHourLocal and < RemindToHourLocal
                             && prefs is not { DmBlocked: true } && prefs is not { QuestRemindOff: true }
                             && sent < MaxRemindersPerTick;
                if (!due || !(finishYesterday || finishToday || remind)) continue;
                LastEval[tg] = now;

                try { await collect.SyncAsync((t ?? y)!.PlayerTag, ct); }
                catch (Exception) when (!ct.IsCancellationRequested) { /* посчитаем по накопленному */ }

                if (finishYesterday)
                {
                    var before = user.FirstOrDefault(r => r.Day == DayOf(now.AddDays(-2), tz));
                    await EvaluateAsync(y!, tz, ct);
                    await CompleteIfDoneAsync(y!, before, ct);
                }

                if (remind)
                {
                    // Сегодня ещё не открывал - заводим день, чтобы было что перечислить
                    t ??= await TodayRowAsync(tg, y!.PlayerTag, y.ClanId, today, ct);
                }
                if (t is { CompletedUtc: null })
                {
                    var items = await EvaluateAsync(t, tz, ct);
                    await CompleteIfDoneAsync(t, y, ct);
                    if (remind && t.CompletedUtc is null && await RemindAsync(tg, prefs, y!.Streak, items, today, ct)) sent++;
                }
                await quests.SaveChangesAsync(ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested) { /* один человек - не повод бросить остальных */ }
        }
        return sent;
    }

    private async Task<bool> RemindAsync(long tg, PlayerAlertPrefs? prefs, int streak, List<ItemDto> items, string today,
        CancellationToken ct)
    {
        if (!await sentLog.TryClaimAsync(RemindKind, $"{tg}:{today}", ct)) return false;
        var player = await players.GetByTelegramIdAsync(tg, ct);
        var lang = player is null ? prefs?.Lang : await TiltMessages.LangAsync(prefs, player, clans, ct);
        var t = QuestText.For(lang);
        var left = items.Where(i => !i.Done).Select(i => "• " + t.Label(i.Key, i.Target)
            + (i.Target > 1 ? $" — {i.Progress}/{i.Target}" : "")).ToList();
        if (left.Count == 0) return false;

        var text = string.Format(t.RemindHead, streak) + "\n\n" + string.Join("\n", left) + "\n\n" + t.RemindTail;
        var r = await sender.SendDmAsync(tg, text, [[new BotButton(t.Button, Url: "startapp:quests")]], false, ct);
        if (r.Blocked)
        {
            var p = await alertPrefs.GetOrCreateAsync(tg, ct);
            p.DmBlocked = true;
            await alertPrefs.SaveChangesAsync(ct);
        }
        if (!r.Delivered) return false;
        await Task.Delay(40, ct);
        return true;
    }
}
