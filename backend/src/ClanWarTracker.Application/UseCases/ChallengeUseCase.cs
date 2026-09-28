using System.Collections.Concurrent;
using System.Text.Json;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.DTOs;
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
    CollectPlayerBattlesUseCase collect)
{
    public const string SettingKey = "challenge.event";

    /// <summary>Каждая такая серия побед подряд даёт бонусный билет.</summary>
    public const int StreakBonusEvery = 3;

    public const int LeadersShown = 50;

    /// <summary>Киевское время по умолчанию: выходные начинаются в субботу 00:00 по Киеву.</summary>
    private const int KyivOffsetMinutes = 180;

    /// <param name="Title">null - название по умолчанию из перевода.</param>
    public record Event(string Id, string? Title, string? Prize, DateTime StartUtc, DateTime EndUtc)
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

    public async Task SaveAsync(Event e, CancellationToken ct = default) =>
        await settings.SetAsync(SettingKey, JsonSerializer.Serialize(e), ct);

    public record Score(int Tickets, int Wins, int Losses, int Streak, int BestStreak, DateTime? LastTicketUtc);

    /// <summary>
    /// Билеты по боям одного игрока (по возрастанию времени). Считаются только ладдер
    /// и Путь легенд: в испытаниях и 2х2 победы стоят другого, а дружеские можно
    /// наиграть с другом. Поражение билет не отнимает, но обрывает серию.
    /// </summary>
    public static Score Compute(IEnumerable<PlayerBattle> list)
    {
        int tickets = 0, wins = 0, losses = 0, streak = 0, best = 0;
        DateTime? last = null;
        foreach (var b in list)
        {
            if (MatchReport.ModeKey(b.Type) is not ("ladder" or "pol")) continue;
            if (b.Result > 0)
            {
                wins++;
                streak++;
                best = Math.Max(best, streak);
                tickets += streak % StreakBonusEvery == 0 ? 2 : 1;
                last = b.BattleTimeUtc;
            }
            else if (b.Result < 0)
            {
                losses++;
                streak = 0;
            }
        }
        return new Score(tickets, wins, losses, streak, best, last);
    }

    public enum JoinOutcome { Ok, NotLinked, Ended }

    public async Task<JoinOutcome> JoinAsync(long tg, CancellationToken ct = default)
    {
        var e = await CurrentAsync(ct);
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
        }
        return JoinOutcome.Ok;
    }

    private record Board(DateTime BuiltUtc, List<ChallengeRowDto> Rows);

    /// <summary>Таблица на несколько секунд: страницу открывают толпой, а считать её каждому незачем.</summary>
    private static readonly ConcurrentDictionary<string, Board> BoardCache = new();
    private static readonly TimeSpan BoardTtl = TimeSpan.FromSeconds(10);

    public async Task<ChallengeDto> GetAsync(long tg, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var e = await CurrentAsync(ct);
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
            new ChallengeEventDto(e.Id, e.Title, e.Prize, e.StartUtc, e.EndUtc, status),
            player is not null, mine is not null, me, leaders, rows.Count, board.BuiltUtc);
    }

    private async Task<Board> BoardAsync(Event e, CancellationToken ct)
    {
        if (BoardCache.TryGetValue(e.Id, out var cached) && DateTime.UtcNow - cached.BuiltUtc < BoardTtl) return cached;

        var list = await entries.GetEntriesAsync(e.Id, ct);
        var tags = list.Select(x => x.PlayerTag).Distinct().ToList();
        List<PlayerBattle> all = tags.Count == 0 ? [] : await battles.GetForTagsBetweenAsync(tags, e.StartUtc, e.EndUtc, ct);
        var byTag = all.GroupBy(b => b.PlayerTag).ToDictionary(g => g.Key, g => g.ToList());

        var scored = list
            .Select(x =>
            {
                var from = x.JoinedUtc > e.StartUtc ? x.JoinedUtc : e.StartUtc;
                var mineBattles = byTag.GetValueOrDefault(x.PlayerTag) ?? [];
                return (Entry: x, Score: Compute(mineBattles.Where(b => b.BattleTimeUtc >= from)));
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
        var e = await CurrentAsync(ct);
        // Полчаса после конца - добрать бои, сыгранные в последние минуты
        if (now < e.StartUtc || now > e.EndUtc.AddMinutes(30)) return 0;

        var due = (await entries.GetEntriesAsync(e.Id, ct))
            .Select(x => x.PlayerTag).Distinct()
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
        if (synced > 0) BoardCache.TryRemove(e.Id, out _);
        return synced;
    }
}
