using System.Collections.Concurrent;
using System.Text.Json;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Уикенд-челлендж: победа в ладдере или Пути легенд - билет, каждая серия из трёх
/// побед подряд - ещё один. Кто больше набрал, тот и победил.
///
/// Билеты не хранятся, а считаются из журнала боёв на каждый запрос: таблица всегда
/// равна тому, что было в игре, и поддельного результата в ней не бывает.
/// </summary>
public class ChallengeUseCase(
    IChallengeRepository entries,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IServiceSettingRepository settings,
    CollectPlayerBattlesUseCase collect,
    TrackerActionsUseCase trackerActions,
    PlusAccess plus,
    IPlayerAlertPrefsRepository alertPrefs,
    ITiltAlertRepository alerts,
    IClanRepository clans,
    INotificationSender sender,
    ICreatorChallengeRepository creatorChallenges,
    IClashRoyaleApi crApi)
{
    public const string SettingKey = "challenge.event";

    /// <summary>Каждая такая серия побед подряд даёт бонусный билет.</summary>
    public const int StreakBonusEvery = 3;

    public const int LeadersShown = 50;

    /// <summary>Киевское время по умолчанию: выходные начинаются в субботу 00:00 по Киеву.</summary>
    private const int KyivOffsetMinutes = 180;

    /// <param name="Title">null - название по умолчанию из перевода.</param>
    /// <param name="GiftPlus">Всем участникам Плюс в подарок до конца челленджа.</param>
    public record Event(string Id, string? Title, string? Prize, DateTime StartUtc, DateTime EndUtc, bool GiftPlus = false,
        string? Rule = null)
    {
        public string Status(DateTime now) => now < StartUtc ? "upcoming" : now <= EndUtc ? "live" : "ended";
    }

    /// <summary>Текущее событие: заданное владельцем, иначе ближайшие выходные.</summary>
    public async Task<Event> CurrentAsync(CancellationToken ct = default)
    {
        var raw = await settings.GetAsync(SettingKey, ct);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var e = JsonSerializer.Deserialize<Event>(raw);
                if (e is not null && e.EndUtc > e.StartUtc) return e;
            }
            catch (JsonException) { /* битая запись - как будто её нет */ }
        }
        return DefaultWeekend(DateTime.UtcNow);
    }

    /// <summary>Эти выходные (если уже идут) или следующие: суббота 00:00 - воскресенье 23:59 по Киеву.</summary>
    public static Event DefaultWeekend(DateTime nowUtc)
    {
        var local = nowUtc.AddMinutes(KyivOffsetMinutes);
        var daysToSat = ((int)DayOfWeek.Saturday - (int)local.DayOfWeek + 7) % 7;
        // Воскресенье - выходные ещё идут, их суббота была вчера
        var saturday = local.DayOfWeek == DayOfWeek.Sunday ? local.Date.AddDays(-1) : local.Date.AddDays(daysToSat);
        var start = DateTime.SpecifyKind(saturday.AddMinutes(-KyivOffsetMinutes), DateTimeKind.Utc);
        return new Event($"weekend-{saturday:yyyyMMdd}", null, "Pass Royale", start, start.AddDays(2).AddSeconds(-1));
    }

    /// <summary>Событие челленджа блогера как обычное событие: подсчёт и таблица те же.</summary>
    public static Event FromCreator(CreatorChallenge c) => new(c.EventId, c.Title, c.Prize, c.StartUtc, c.EndUtc,
        Rule: ChallengeRules.Normalize(c.Rule));

    /// <summary>Событие по коду блогера, без кода - общий челлендж. null - кода такого нет.</summary>
    private async Task<(Event? Event, CreatorChallenge? Creator)> EventAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return (await CurrentAsync(ct), null);
        var c = await creatorChallenges.GetByCodeAsync(code.Trim(), ct);
        return c is null ? (null, null) : (FromCreator(c), c);
    }

    public async Task SaveAsync(Event e, CancellationToken ct = default) =>
        await settings.SetAsync(SettingKey, JsonSerializer.Serialize(e), ct);

    public record Score(int Tickets, int Wins, int Losses, int Streak, int BestStreak, DateTime? LastTicketUtc);

    /// <summary>
    /// Билеты по боям одного игрока (по возрастанию времени). Считаются только ладдер
    /// и Путь легенд: в испытаниях и 2х2 победы стоят другого, а дружеские можно
    /// наиграть с другом. Поражение билет не отнимает, но обрывает серию.
    /// </summary>
    /// <param name="rule">Формат челленджа (ChallengeRules); null - билеты.</param>
    /// <param name="deckElixir">Средний эликсир колоды по её ключу - нужен формату «тяжёлая колода».</param>
    public static Score Compute(IEnumerable<PlayerBattle> list, string? rule = null, Func<string, double?>? deckElixir = null)
    {
        rule = ChallengeRules.Normalize(rule);
        int tickets = 0, wins = 0, losses = 0, streak = 0, best = 0;
        DateTime? last = null;
        foreach (var b in list)
        {
            var mode = MatchReport.ModeKey(b.Type);
            if (mode is not ("ladder" or "pol")) continue;
            if (b.Result > 0)
            {
                wins++;
                streak++;
                var before = best;
                best = Math.Max(best, streak);
                var points = rule switch
                {
                    ChallengeRules.Tickets => streak % StreakBonusEvery == 0 ? 2 : 1,
                    ChallengeRules.ThreeCrowns => b.CrownsFor >= 3 ? 1 : 0,
                    // Соперник не взял ни одной короны - значит, ни одна башня не упала
                    ChallengeRules.Flawless => b.CrownsAgainst == 0 ? 1 : 0,
                    // Счёт - сама лучшая серия: растёт, только когда серия её побила
                    ChallengeRules.Streak => best - before,
                    // Неизвестная стоимость (карты нет в справочнике) - не засчитываем: лучше
                    // пропустить, чем дать очко колоде, которую не смогли проверить
                    ChallengeRules.Heavy => deckElixir?.Invoke(b.DeckKey) is double avg
                                            && avg > ChallengeRules.HeavyMinAvgElixir ? 1 : 0,
                    _ => 1,
                };
                tickets += points;
                if (points > 0) last = b.BattleTimeUtc;
            }
            else if (b.Result < 0)
            {
                losses++;
                streak = 0;
            }
        }
        return new Score(tickets, wins, losses, streak, best, last);
    }

    public enum JoinOutcome { Ok, NotLinked, Ended, NotFound }

    public async Task<JoinOutcome> JoinAsync(long tg, string? code = null, CancellationToken ct = default)
    {
        var (e, _) = await EventAsync(code, ct);
        if (e is null) return JoinOutcome.NotFound;
        if (e.Status(DateTime.UtcNow) == "ended") return JoinOutcome.Ended;
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return JoinOutcome.NotLinked;
        if (await entries.GetEntryAsync(e.Id, tg, ct) is null)
        {
            await entries.AddAsync(new ChallengeEntry
            {
                EventId = e.Id,
                TelegramUserId = tg,
                PlayerTag = player.PlayerTag,
                Name = player.Name.Length > 64 ? player.Name[..64] : player.Name,
                JoinedUtc = DateTime.UtcNow,
            }, ct);
            BoardCache.TryRemove(e.Id, out _);

            // Трекер - сразу: после каждого боя карточка с билетами и местом, и человек
            // видит бота все выходные, а не только когда вспомнит открыть таблицу.
            try { await trackerActions.SetAsync(tg, true, null, null, notify: true, ct); }
            catch { /* без трекера челлендж всё равно работает */ }
            if (e.GiftPlus)
            {
                try { await GrantGiftAsync(e, tg, player.PlayerTag, ct); }
                catch { /* подарок не вышел - вступление всё равно засчитано */ }
            }
        }
        return JoinOutcome.Ok;
    }

    /// <summary>Плюс в подарок до конца челленджа плюс пара часов - чтобы застать итог.</summary>
    private async Task<bool> GrantGiftAsync(Event e, long tg, string tag, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var giftUntil = e.EndUtc.AddHours(2);
        var status = await plus.GetAsync(tg, ct);
        var from = status.Active && status.Until is DateTime u && u > now ? u : now;
        if (from >= giftUntil) return false;
        var days = (int)Math.Ceiling((giftUntil - from).TotalDays);
        if (days < 1) return false;
        await plus.GrantAsync(tg, days, Entitlement.Sources.Grant, tag, 0, null, ct);
        return true;
    }

    /// <summary>
    /// Владелец включил «Плюс в подарок»: выдать всем, кто уже в челлендже, и дальше
    /// выдавать каждому вступившему. Возвращает, скольким выдано сейчас.
    /// </summary>
    public async Task<int> GiftPlusAsync(CancellationToken ct = default)
    {
        var e = await CurrentAsync(ct);
        if (e.Status(DateTime.UtcNow) == "ended") return 0;
        if (!e.GiftPlus)
        {
            e = e with { GiftPlus = true };
            await SaveAsync(e, ct);
        }
        var n = 0;
        foreach (var x in (await entries.GetEntriesAsync(e.Id, ct)).DistinctBy(x => x.TelegramUserId))
        {
            try { if (await GrantGiftAsync(e, x.TelegramUserId, x.PlayerTag, ct)) n++; }
            catch { /* один сбой - остальным всё равно выдаём */ }
        }
        return n;
    }

    /// <param name="GapUp">Сколько билетов нужно, чтобы обойти того, кто выше. 0 - ты первый.</param>
    public record Standing(int Tickets, int Rank, int Total, int GapUp);

    /// <summary>Место в таблице для карточки трекера. null - челлендж не идёт или человек не участвует.</summary>
    public async Task<Standing?> StandingAsync(long tg, CancellationToken ct = default)
    {
        var e = await CurrentAsync(ct);
        if (e.Status(DateTime.UtcNow) != "live") return null;
        var mine = await entries.GetEntryAsync(e.Id, tg, ct);
        if (mine is null) return null;
        // Свежая таблица: карточку строят сразу после чтения журнала, и билет за этот бой должен в неё попасть
        BoardCache.TryRemove(e.Id, out _);
        var board = await BoardAsync(e, ct);
        var row = board.Rows.FirstOrDefault(r => r.Tag == mine.PlayerTag);
        if (row is null) return null;
        var gap = row.Rank > 1 ? board.Rows[row.Rank - 2].Tickets - row.Tickets + 1 : 0;
        return new Standing(row.Tickets, row.Rank, board.Rows.Count, Math.Max(1, gap) * (row.Rank > 1 ? 1 : 0));
    }

    public const string PromoPrice7Key = "promo.price7";
    public const string PromoPrice30Key = "promo.price30";
    public const int DefaultPromo7 = 29;
    public const int DefaultPromo30 = 69;

    /// <summary>
    /// Итог челленджа - одно сообщение каждому участнику, раз за событие: место, билеты,
    /// что сделал Стоп-тильт, и личная скидка на Плюс до среды. Пишем им, потому что они
    /// сами вступили, - это не рассылка всем подряд.
    /// </summary>
    public async Task<int> FinishAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var e = await CurrentAsync(ct);
        // Полчаса после конца - добрать последние бои; дальше двух суток итог уже не нужен
        if (now < e.EndUtc.AddMinutes(40) || now > e.EndUtc.AddDays(2)) return 0;
        var doneKey = "challenge.finished." + e.Id;
        if (!string.IsNullOrEmpty(await settings.GetAsync(doneKey, ct))) return 0;
        // Отметка - до рассылки: упадём посередине - второй раз никому не напишем
        await settings.SetAsync(doneKey, now.ToString("O"), ct);

        BoardCache.TryRemove(e.Id, out _);
        var board = await BoardAsync(e, ct);
        var list = (await entries.GetEntriesAsync(e.Id, ct)).DistinctBy(x => x.TelegramUserId).ToList();
        var offer = await PlusSales.ReadAsync(settings, ct);
        var p7 = await IntSettingAsync(PromoPrice7Key, DefaultPromo7, ct);
        var p30 = await IntSettingAsync(PromoPrice30Key, DefaultPromo30, ct);
        var promoUntil = WednesdayEnd(e.EndUtc);
        var winner = board.Rows.FirstOrDefault(r => r.Tickets > 0);
        var sent = 0;

        foreach (var x in list)
        {
            try
            {
                var tg = x.TelegramUserId;
                var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
                if (prefs.DmBlocked) continue;
                var player = await players.GetByTelegramIdAsync(tg, ct);
                var t = BotText.For(player is null ? prefs.Lang : await TiltMessages.LangAsync(prefs, player, clans, ct));
                var tz = prefs.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
                var row = board.Rows.FirstOrDefault(r => r.Tag == x.PlayerTag);

                var lines = new List<string> { t.ChEndHead };
                lines.Add(row is { Tickets: > 0 }
                    ? string.Format(t.ChEndPlace, row.Tickets, row.Rank, board.Rows.Count)
                    : t.ChEndNoTickets);
                if (winner is not null)
                    lines.Add(winner.Tag == x.PlayerTag ? t.ChEndYouWon : string.Format(t.ChEndWinner, winner.Name, winner.Tickets));

                // Что сделал Стоп-тильт за выходные - лучший довод продлить
                var tilt = (await alerts.GetSinceAsync(tg, e.StartUtc, ct))
                    .Where(a => a.Kind == "streak" && a.SentUtc <= e.EndUtc.AddMinutes(30) && a.MessageId is not null).ToList();
                if (tilt.Count > 0)
                {
                    var paused = tilt.Where(a => a.Choice == "pause").ToList();
                    var line = string.Format(t.ChEndTilt, tilt.Count);
                    if (paused.Count > 0)
                        line += string.Format(t.ChEndTiltPause, paused.Sum(a => a.AfterWins), paused.Sum(a => a.AfterLosses));
                    lines.Add(line);
                }

                var rows = new List<IReadOnlyList<BotButton>> { new[] { new BotButton(t.ChBtnResults, Url: "startapp:challenge") } };
                var status = await plus.GetAsync(tg, ct);
                // Скидку - тем, у кого нет своего оплаченного Плюса надолго вперёд
                if (offer.OnSale && !(status.Active && status.Until > now.AddDays(2)) && p7 < offer.Price7)
                {
                    prefs.PromoPrice7 = p7;
                    prefs.PromoPrice30 = p30;
                    prefs.PromoUntilUtc = promoUntil;
                    await alertPrefs.SaveChangesAsync(ct);
                    lines.Add("");
                    lines.Add(string.Format(e.GiftPlus ? t.ChEndPromoGift : t.ChEndPromo,
                        p7, offer.Price7, p30, offer.Price30, promoUntil.AddMinutes(tz).ToString("dd.MM HH:mm")));
                    rows.Add(new[] { new BotButton(t.ChBtnPromo, Url: "startapp:plus") });
                }

                var r = await sender.SendDmAsync(tg, string.Join("\n", lines), rows, false, ct);
                if (r.Blocked)
                {
                    prefs.DmBlocked = true;
                    await alertPrefs.SaveChangesAsync(ct);
                }
                else if (r.Delivered) sent++;
                await Task.Delay(40, ct);
            }
            catch { /* один сбой - остальным итог всё равно уходит */ }
        }
        return sent;
    }

    private async Task<int> IntSettingAsync(string key, int fallback, CancellationToken ct) =>
        int.TryParse(await settings.GetAsync(key, ct), out var v) && v > 0 ? v : fallback;

    /// <summary>Среда 23:59 по Киеву после конца челленджа - дедлайн скидки.</summary>
    public static DateTime WednesdayEnd(DateTime endUtc)
    {
        var local = endUtc.AddMinutes(KyivOffsetMinutes);
        var days = ((int)DayOfWeek.Wednesday - (int)local.DayOfWeek + 7) % 7;
        if (days == 0) days = 7;
        var wed = local.Date.AddDays(days).AddHours(23).AddMinutes(59);
        return DateTime.SpecifyKind(wed.AddMinutes(-KyivOffsetMinutes), DateTimeKind.Utc);
    }

    private record Board(DateTime BuiltUtc, List<ChallengeRowDto> Rows);

    /// <summary>Таблица на несколько секунд: страницу открывают толпой, а считать её каждому незачем.</summary>
    private static readonly ConcurrentDictionary<string, Board> BoardCache = new();
    private static readonly TimeSpan BoardTtl = TimeSpan.FromSeconds(10);

    public async Task<ChallengeDto?> GetAsync(long tg, string? code = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var (e, creator) = await EventAsync(code, ct);
        if (e is null) return null;
        var status = e.Status(now);
        var player = await players.GetByTelegramIdAsync(tg, ct);
        var mine = await entries.GetEntryAsync(e.Id, tg, ct);

        // Своя строка - самая свежая: открыл страницу сразу после боя - и билет уже там.
        if (mine is not null && status == "live")
        {
            try
            {
                if (await collect.SyncFreshAsync(mine.PlayerTag, ct) > 0) BoardCache.TryRemove(e.Id, out _);
            }
            catch { /* API игры недоступен - покажем накопленное */ }
        }

        var board = await BoardAsync(e, ct);
        var rows = board.Rows.Select(r => r with { IsMe = mine is not null && r.Tag == mine.PlayerTag }).ToList();
        var me = rows.FirstOrDefault(r => r.IsMe);
        var leaders = rows.Take(LeadersShown).ToList();

        return new ChallengeDto(
            new ChallengeEventDto(e.Id, e.Title, e.Prize, e.StartUtc, e.EndUtc, status, e.GiftPlus,
                Code: creator?.Code, Host: creator?.CreatorName, Rule: ChallengeRules.Normalize(e.Rule)),
            player is not null, mine is not null, me, leaders, rows.Count, board.BuiltUtc);
    }

    private async Task<Board> BoardAsync(Event e, CancellationToken ct)
    {
        if (BoardCache.TryGetValue(e.Id, out var cached) && DateTime.UtcNow - cached.BuiltUtc < BoardTtl) return cached;

        var list = await entries.GetEntriesAsync(e.Id, ct);
        var deckElixir = ChallengeRules.Normalize(e.Rule) == ChallengeRules.Heavy ? await DeckElixirAsync(ct) : null;
        var tags = list.Select(x => x.PlayerTag).Distinct().ToList();
        List<PlayerBattle> all = tags.Count == 0 ? [] : await battles.GetForTagsBetweenAsync(tags, e.StartUtc, e.EndUtc, ct);
        var byTag = all.GroupBy(b => b.PlayerTag).ToDictionary(g => g.Key, g => g.ToList());

        var scored = list
            .Select(x =>
            {
                var from = x.JoinedUtc > e.StartUtc ? x.JoinedUtc : e.StartUtc;
                var mineBattles = byTag.GetValueOrDefault(x.PlayerTag) ?? [];
                return (Entry: x, Score: Compute(mineBattles.Where(b => b.BattleTimeUtc >= from), e.Rule, deckElixir));
            })
            // Больше билетов; поровну - больше побед; поровну - кто набрал раньше
            .OrderByDescending(s => s.Score.Tickets)
            .ThenByDescending(s => s.Score.Wins)
            .ThenBy(s => s.Score.LastTicketUtc ?? DateTime.MaxValue)
            .ThenBy(s => s.Entry.JoinedUtc)
            .ToList();

        var rows = scored.Select((s, i) => new ChallengeRowDto(
            i + 1, s.Entry.Name, s.Entry.PlayerTag, s.Score.Tickets, s.Score.Wins, s.Score.Losses,
            s.Score.Streak, s.Score.BestStreak, false)).ToList();

        var board = new Board(DateTime.UtcNow, rows);
        BoardCache[e.Id] = board;
        return board;
    }

    /// <summary>
    /// Средний эликсир колоды по ключу (id карт, эволюция - с минусом). null - справочник
    /// недоступен или в колоде карта, которой в нём нет.
    /// </summary>
    private async Task<Func<string, double?>?> DeckElixirAsync(CancellationToken ct)
    {
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        if (catalog.Count == 0) return null;
        return deckKey =>
        {
            var keys = MetaCard.ParseDeckKey(deckKey);
            if (keys.Length == 0) return null;
            var total = 0;
            foreach (var k in keys)
            {
                if (!catalog.TryGetValue(Math.Abs(k), out var card) || card.ElixirCost <= 0) return null;
                total += card.ElixirCost;
            }
            return (double)total / keys.Length;
        };
    }

    /// <summary>Когда журнал участника читали последний раз - чтобы идти по кругу, а не читать всех разом.</summary>
    private static readonly ConcurrentDictionary<string, DateTime> LastSync = new();

    /// <summary>
    /// Для воркера: пока идёт челлендж, по кругу подтягивает бои участников, чтобы
    /// таблица жила и у тех, кто страницу не открывает. Не больше <paramref name="limit"/>
    /// журналов за проход - остальные в следующую минуту.
    /// </summary>
    public async Task<int> SyncAsync(int limit, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Общий челлендж и идущие челленджи блогеров - журналы одни и те же, читаем
        // участников всех событий одним кругом.
        var grace = TimeSpan.FromMinutes(30);
        var events = new List<Event>();
        var current = await CurrentAsync(ct);
        // Полчаса после конца - добрать бои, сыгранные в последние минуты
        if (now >= current.StartUtc && now <= current.EndUtc + grace) events.Add(current);
        events.AddRange((await creatorChallenges.GetRunningAsync(now, grace, ct)).Select(FromCreator));
        if (events.Count == 0) return 0;

        var tags = new List<string>();
        foreach (var ev in events) tags.AddRange((await entries.GetEntriesAsync(ev.Id, ct)).Select(x => x.PlayerTag));

        var due = tags.Distinct()
            .Where(tag => now - LastSync.GetValueOrDefault(tag) >= TimeSpan.FromMinutes(2))
            .OrderBy(tag => LastSync.GetValueOrDefault(tag))
            .Take(limit)
            .ToList();

        var synced = 0;
        foreach (var tag in due)
        {
            try
            {
                await collect.SyncFreshAsync(tag, ct);
                synced++;
            }
            catch { /* один журнал не открылся - остальные дальше */ }
            LastSync[tag] = now;
        }
        if (synced > 0) foreach (var ev in events) BoardCache.TryRemove(ev.Id, out _);
        return synced;
    }
}
