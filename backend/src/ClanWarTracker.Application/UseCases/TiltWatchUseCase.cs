using System.Collections.Concurrent;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Стоп-тильт»: пишет игроку прямо во время серии поражений и подводит итог захода.
///
/// Главное здесь - успеть. Сигнал, пришедший посреди четвёртого боя, стоит тех самых
/// кубков, которые он должен был сберечь. Поэтому журнал «горячих» игроков (последний
/// бой не старше 15 минут) читается раз в полторы минуты почти без кэша, остальных -
/// раз в десять минут: так видно, что человек сел играть.
///
/// Кому пишем: у кого Плюс (или спонсорство), и кто не выключил; без Плюса - тем, кто
/// сам включил «Стоп-тильт» и ещё не израсходовал бесплатные сигналы. Один сигнал за
/// заход, не больше трёх в сутки, ночью молчим.
/// </summary>
public class TiltWatchUseCase(
    IEntitlementRepository entitlements,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IPlayerBattleRepository battles,
    ITiltAlertRepository alerts,
    IClanRepository clans,
    IClashRoyaleApi crApi,
    CollectPlayerBattlesUseCase collect,
    IServiceSettingRepository settings,
    BattleTrackerUseCase tracker,
    INotificationSender sender)
{
    /// <summary>Последний бой не старше этого - человек ещё в игре.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(15);

    /// <summary>Как часто перечитываем журнал того, кто сейчас играет.</summary>
    public static readonly TimeSpan HotPoll = TimeSpan.FromSeconds(90);

    /// <summary>Как часто - всех остальных: чтобы заметить начало захода.</summary>
    public static readonly TimeSpan ColdPoll = TimeSpan.FromMinutes(10);

    /// <summary>Столько без боёв - заход закончен, пора подводить итог.</summary>
    public static readonly TimeSpan SessionEnd = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan PauseLength = TimeSpan.FromMinutes(15);

    public const int MaxAlertsPerDay = 3;

    /// <summary>
    /// Сколько «холодных» чтений журнала за проход. После рестарта воркера память о
    /// последнем чтении пуста, и без предела первый проход прочитал бы всех разом.
    /// </summary>
    public const int MaxColdSyncsPerTick = 60;

    /// <summary>
    /// Когда журнал игрока читали последний раз. В памяти процесса, а не в базе: после
    /// рестарта воркера лишнее чтение стоит одного запроса, а запись в базу на каждый
    /// опрос стоила бы сотен.
    /// </summary>
    private static readonly ConcurrentDictionary<long, DateTime> LastSync = new();

    public record Summary(
        int Watched, int Synced, int Alerts, int Undelivered, int Resumes, int Summaries,
        int Cards = 0, int CardEdits = 0, int TrackerSummaries = 0);

    private sealed class Counters
    {
        public int Synced, ColdSyncs, Alerts, Undelivered, Resumes, Summaries, Cards, CardEdits, TrackerSummaries;

        /// <summary>Платное включено. Выключено владельцем - строки Плюса в трекере видят все.</summary>
        public bool Paywall = true;
    }

    public async Task<Summary> ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var offer = await PlusSales.ReadAsync(settings, ct);

        // Платные: Плюс, купленный или подаренный, и спонсоры - им Плюс входит в спонсорство.
        var paid = new HashSet<long>(await entitlements.ActiveUsersAsync(Entitlement.Skus.Plus, now, ct));
        foreach (var p in await players.GetAllLinkedAsync(ct))
            if (p.TelegramUserId is long tg && p.IsSponsor(now)) paid.Add(tg);
        // Платное выключено владельцем - всё открыто: пишем всем, кто включил сам.
        if (!offer.Paywall) paid.UnionWith(await alertPrefs.OptedInAsync(ct));

        var watch = new HashSet<long>(paid);
        watch.UnionWith(await alertPrefs.FreeSignalUsersAsync(ct));
        // Незакрытые итоги и паузы дописываем даже тем, у кого Плюс уже кончился.
        watch.UnionWith(await alerts.UsersWithOpenAsync(ct));
        watch.UnionWith(await alertPrefs.PausedUsersAsync(ct));
        // Трекер боёв - тот же опрос: без Плюса тоже, это не платная функция.
        var beta = await BattleTrackerUseCase.BetaAsync(settings, ct);
        watch.UnionWith((await alertPrefs.TrackerUsersAsync(ct)).Where(tg => BattleTrackerUseCase.Allowed(beta, tg)));

        var c = new Counters { Paywall = offer.Paywall };
        foreach (var tg in watch)
        {
            try { await CheckOneAsync(tg, paid.Contains(tg), BattleTrackerUseCase.Allowed(beta, tg), now, c, ct); }
            catch { /* один сломанный журнал не должен лишать сигналов остальных */ }
        }
        return new Summary(watch.Count, c.Synced, c.Alerts, c.Undelivered, c.Resumes, c.Summaries,
            c.Cards, c.CardEdits, c.TrackerSummaries);
    }

    private async Task CheckOneAsync(long tg, bool paid, bool trackerAllowed, DateTime now, Counters c, CancellationToken ct)
    {
        var prefs = await alertPrefs.GetAsync(tg, ct);
        if (prefs is { DmBlocked: true }) return;

        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return;
        var tag = player.PlayerTag;

        var open = await alerts.GetOpenAsync(tg, ct);
        var canAlert = paid
            ? prefs?.TiltAlerts != false
            : prefs is { TiltAlerts: true, FreeSignalsLeft: > 0 };
        var tracking = trackerAllowed && BattleTrackerUseCase.Tracking(prefs);
        if (!canAlert && open.Count == 0 && prefs?.PauseUntilUtc is null && !tracking) return;

        // Читаем журнал: часто - пока человек играет, редко - пока нет.
        var recent = await battles.GetSinceAsync(tag, now.AddHours(-6), ct);
        var hot = (recent.Count > 0 && now - recent[^1].BattleTimeUtc <= Freshness)
                  || open.Count > 0 || prefs?.TrackerCardMessageId is not null;
        var last = LastSync.GetValueOrDefault(tg);
        if (now - last >= (hot ? HotPoll : ColdPoll) && (hot || c.ColdSyncs < MaxColdSyncsPerTick))
        {
            try
            {
                if (!hot) c.ColdSyncs++;
                await collect.SyncFreshAsync(tag, ct);
                LastSync[tg] = now;
                c.Synced++;
                recent = await battles.GetSinceAsync(tag, now.AddHours(-6), ct);
            }
            catch { /* API игры недоступен - работаем с тем, что уже есть */ }
        }
        var eligible = recent.Where(b => BattleAnalyzer.IsTiltEligible(b.Type)).ToList();

        var lang = await TiltMessages.LangAsync(prefs, player, clans, ct);
        var t = BotText.For(lang);
        var tz = prefs?.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
        List<PlayerBattle>? month = null;
        async Task<List<PlayerBattle>> MonthAsync() => month ??= (await battles.GetSinceAsync(
                tag, now - CollectPlayerBattlesUseCase.Keep, ct))
            .Where(b => BattleAnalyzer.IsTiltEligible(b.Type)).ToList();

        // 1. Пауза кончилась - «можно», и с какой колоды начать.
        if (prefs?.PauseUntilUtc is { } pauseUntil && pauseUntil <= now)
        {
            prefs.PauseUntilUtc = null;
            var text = await ResumeTextAsync(t, await MonthAsync(), ct);
            await sender.SendToUserWithButtonsAsync(tg, text, [], ct);
            foreach (var a in open.Where(a => a.Choice == "pause" && a.ResumeSentUtc is null))
                a.ResumeSentUtc = now;
            c.Resumes++;
        }

        // 2. Заход закончился - итог правкой того же сообщения, без нового уведомления.
        foreach (var a in open)
        {
            var session = BattleAnalyzer.SessionAround(eligible, a.TriggerBattleUtc);
            if (session.Count == 0)
            {
                // Бой-причина выпал из окна: итог уже не посчитать честно, просто закрываем.
                if (now - a.SentUtc > TimeSpan.FromHours(6)) a.SummaryUtc = now;
                continue;
            }
            if (now - session[^1].BattleTimeUtc < SessionEnd) continue;

            a.SessionWins = session.Count(b => b.Result > 0);
            a.SessionLosses = session.Count(b => b.Result < 0);
            a.SessionTrophies = session.Sum(b => b.TrophyChange ?? 0);
            var after = session.Where(b => b.BattleTimeUtc > a.TriggerBattleUtc).ToList();
            a.AfterWins = after.Count(b => b.Result > 0);
            a.AfterLosses = after.Count(b => b.Result < 0);
            a.SummaryUtc = now;

            if (a.Kind == "streak" && a.MessageId is int messageId)
            {
                var works = await PauseWorksAsync(tg, a, now, ct);
                var summaryLang = BotText.For(a.Lang);
                await sender.EditUserMessageAsync(tg, messageId, TiltMessages.Summary(summaryLang, a, works), null, ct);
            }
            c.Summaries++;
        }

        // 2½. Трекер боёв: карточка захода. До сигнала - чтобы сигнал знал строку о последнем бое.
        string? alertPrefix = null;
        if (tracking)
        {
            var threshold = Math.Clamp(prefs!.LossThreshold, 2, 3);
            var tick = await tracker.StepAsync(
                new BattleTrackerUseCase.Ctx(tg, player, prefs, paid || !c.Paywall, recent, t, tz, now, threshold), ct);
            alertPrefix = tick.AlertPrefix;
            if (tick.Created) c.Cards++;
            if (tick.Edited) c.CardEdits++;
            if (tick.Summarized) c.TrackerSummaries++;
        }

        // 3. Новый сигнал.
        if (canAlert) await TryAlertAsync(tg, player, prefs, paid, eligible, t, lang, tz, now, MonthAsync, c, ct, alertPrefix);

        // 4. «Лимит на вечер» - только с Плюсом: это правило, которое человек поставил себе сам.
        if (paid && prefs is { DailyLossLimit: int limit } && canAlert)
            await TryLimitAsync(tg, player, prefs, limit, t, lang, tz, now, c, ct);

        await alerts.SaveChangesAsync(ct);
    }

    private async Task TryAlertAsync(
        long tg, Player player, PlayerAlertPrefs? prefs, bool paid, List<PlayerBattle> eligible,
        BotText t, string lang, int tz, DateTime now, Func<Task<List<PlayerBattle>>> monthAsync,
        Counters c, CancellationToken ct, string? prefix = null)
    {
        if (prefs?.PauseUntilUtc > now || prefs?.MutedUntilUtc > now) return;
        if ((prefs?.QuietHours ?? true) && TiltMessages.IsQuietHour(now, tz)) return;

        var threshold = Math.Clamp(prefs?.LossThreshold ?? 2, 2, 3);
        var check = BattleAnalyzer.ShouldAlertTilt(eligible, now, prefs?.LastAlertBattleUtc, Freshness, threshold);
        if (!check.Alert || check.LastBattleUtc is not DateTime trigger) return;

        var today = await alerts.GetSinceAsync(tg, TiltMessages.LocalMidnightUtc(now, tz), ct);
        if (today.Count(a => a.Kind == "streak") >= MaxAlertsPerDay) return;

        // Бои серии - с конца, ничьи пропускаем: по ним и кубки, и «каждый раз был Хог».
        var streakBattles = new List<PlayerBattle>();
        for (var i = eligible.Count - 1; i >= 0 && streakBattles.Count < check.LossStreak; i--)
        {
            if (eligible[i].Result > 0) break;
            if (eligible[i].Result < 0) streakBattles.Add(eligible[i]);
        }
        int? trophiesLost = streakBattles.All(b => b.TrophyChange is not null)
            ? streakBattles.Sum(b => b.TrophyChange!.Value)
            : null;
        var sameCard = await SameCardAsync(streakBattles, ct);

        var profile = BattleAnalyzer.Profile(await monthAsync());
        int? freeLeftAfter = paid ? null : Math.Max(0, (prefs?.FreeSignalsLeft ?? 0) - 1);
        var text = TiltMessages.Alert(t, check.LossStreak, trophiesLost, profile, sameCard,
            now.DayOfYear + today.Count, freeLeftAfter);
        // С трекером сигнал начинается с того самого боя: «Бой 4: 0–1 против Golem · −29🏆».
        if (prefix is not null) text = prefix + "\n\n" + text;

        var session = BattleAnalyzer.SessionAround(eligible, trigger);
        var alert = new TiltAlert
        {
            TelegramUserId = tg,
            PlayerTag = player.PlayerTag,
            Kind = "streak",
            SentUtc = now,
            TriggerBattleUtc = trigger,
            SessionStartUtc = session.Count > 0 ? session[0].BattleTimeUtc : trigger,
            LossStreak = check.LossStreak,
            Free = !paid,
            Lang = lang,
            Text = text,
        };
        await alerts.AddAsync(alert, ct);
        await alerts.SaveChangesAsync(ct);   // номер сигнала нужен кнопкам

        var sent = await sender.SendDmAsync(tg, text, TiltMessages.AlertButtons(t, alert.Id), false, ct);

        prefs ??= await alertPrefs.GetOrCreateAsync(tg, ct);
        prefs.LastAlertBattleUtc = trigger;
        if (sent.MessageId is int id)
        {
            alert.MessageId = id;
            prefs.LastAlertSentUtc = now;
            if (!paid) prefs.FreeSignalsLeft = Math.Max(0, prefs.FreeSignalsLeft - 1);
            c.Alerts++;
        }
        else
        {
            // Не запускал бота или заблокировал - не пытаемся, пока не вернётся. Временный
            // сбой Telegram - просто пропускаем эту серию, не замолкая навсегда.
            if (sent.Blocked) prefs.DmBlocked = true;
            alert.SummaryUtc = now;
            c.Undelivered++;
        }
    }

    private async Task TryLimitAsync(
        long tg, Player player, PlayerAlertPrefs prefs, int limit, BotText t, string lang, int tz, DateTime now,
        Counters c, CancellationToken ct)
    {
        var day = now.AddMinutes(tz).ToString("yyyy-MM-dd");
        if (prefs.LimitAlertDay == day || prefs.MutedUntilUtc > now) return;
        if (prefs.QuietHours && TiltMessages.IsQuietHour(now, tz)) return;

        var todayBattles = (await battles.GetSinceAsync(player.PlayerTag, TiltMessages.LocalMidnightUtc(now, tz), ct))
            .Where(b => BattleAnalyzer.IsTiltEligible(b.Type)).ToList();
        if (todayBattles.Count == 0 || now - todayBattles[^1].BattleTimeUtc > Freshness) return;

        var losses = todayBattles.Count(b => b.Result < 0);
        if (losses < limit) return;

        var text = string.Format(t.TiltLimitHead, losses) + "\n" + t.TiltLimitBody;
        var alert = new TiltAlert
        {
            TelegramUserId = tg,
            PlayerTag = player.PlayerTag,
            Kind = "limit",
            SentUtc = now,
            TriggerBattleUtc = todayBattles[^1].BattleTimeUtc,
            SessionStartUtc = todayBattles[0].BattleTimeUtc,
            LossStreak = 0,
            Lang = lang,
            Text = text,
            // Итога у лимита нет: он про весь день, а не про заход.
            SummaryUtc = now,
        };
        await alerts.AddAsync(alert, ct);
        await alerts.SaveChangesAsync(ct);

        var sent = await sender.SendDmAsync(tg, text, [[new BotButton(t.TiltBtnMute, $"tilt|m|{alert.Id}")]], false, ct);
        alert.MessageId = sent.MessageId;
        prefs.LimitAlertDay = day;
        if (sent.Blocked) prefs.DmBlocked = true;
        else if (sent.Delivered) c.Alerts++;
    }

    /// <summary>
    /// Карта, которая была у соперника в каждом бою серии. Из общих - самая дорогая:
    /// дешёвые заклинания есть почти у всех, а «каждый раз был Хог» - это уже наблюдение.
    /// </summary>
    private async Task<string?> SameCardAsync(List<PlayerBattle> streak, CancellationToken ct)
    {
        if (streak.Count < 2) return null;
        HashSet<int>? common = null;
        foreach (var b in streak)
        {
            var ids = MetaCard.ParseDeckKey(b.OppDeckKey).Select(k => Math.Abs(k)).ToHashSet();
            if (common is null) common = ids;
            else common.IntersectWith(ids);
        }
        if (common is not { Count: > 0 }) return null;

        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        return common
            .Select(id => catalog.GetValueOrDefault(id))
            .Where(card => card is not null)
            .OrderByDescending(card => card!.ElixirCost)
            .Select(card => card!.Name)
            .FirstOrDefault();
    }

    /// <summary>«🟢 Можно. Начни с колоды с «Хогом» — 58% за месяц.»</summary>
    private async Task<string> ResumeTextAsync(BotText t, List<PlayerBattle> month, CancellationToken ct)
    {
        if (BattleAnalyzer.BestDeck(month) is not { } best) return t.TiltResumeGeneric;

        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var keyCard = MetaCard.ParseDeckKey(best.Key)
            .Select(k => catalog.GetValueOrDefault(Math.Abs(k)))
            .Where(card => card is not null)
            .OrderByDescending(card => card!.ElixirCost)
            .Select(card => card!.Name)
            .FirstOrDefault();
        return keyCard is null
            ? t.TiltResumeGeneric
            : string.Format(t.TiltResume, keyCard, best.Percent.ToString("0"));
    }

    /// <summary>
    /// «Пауза работает»: % побед после сигналов с паузой и без неё. Показываем, когда
    /// сигналов не меньше пяти и в каждой группе есть хотя бы три боя - на меньшем
    /// любая разница случайна, а неправда в главном доводе хуже его отсутствия.
    /// </summary>
    private async Task<(double Paused, double NotPaused)?> PauseWorksAsync(
        long tg, TiltAlert current, DateTime now, CancellationToken ct)
    {
        var history = (await alerts.GetSinceAsync(tg, now.AddDays(-60), ct))
            .Where(a => a.Kind == "streak" && a.SummaryUtc is not null && a.Id != current.Id)
            .Append(current)
            .ToList();
        if (history.Count < 5) return null;

        var paused = history.Where(a => a.Choice == "pause").ToList();
        var other = history.Where(a => a.Choice != "pause").ToList();
        int pw = paused.Sum(a => a.AfterWins), pl = paused.Sum(a => a.AfterLosses);
        int ow = other.Sum(a => a.AfterWins), ol = other.Sum(a => a.AfterLosses);
        if (pw + pl < 3 || ow + ol < 3) return null;
        return (100.0 * pw / (pw + pl), 100.0 * ow / (ow + ol));
    }
}
