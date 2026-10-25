using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Штаб клана»: кто из реальных игроков хуже всех воюет и на что обратить внимание.
///
/// «Реальный» - важное слово. В любом клане есть твинки и гости: заходят на войну,
/// отыгрывают и уходят, или в клане вторую неделю. Их слабые цифры - не проблема
/// клана, а шум, и если показывать их наравне со всеми, глава перестанет верить списку.
/// Поэтому в разбор попадает только текущий состав, сыгравший хотя бы две недели из
/// разобранных и не меньше трёх дней войны колодами.
/// </summary>
public class GetClanHqUseCase(
    IClashRoyaleApi crApi,
    IWarSnapshotRepository snapshots,
    IWarBattleRepository warBattles,
    GetClanDisciplineUseCase discipline,
    IMemoryCache cache)
{
    private const int WeeksWindow = 6;
    private const int WarDecksPerWeek = 16;
    private const int MinWeeksPlayed = 2;
    private const int MinDecks = 12;
    private const int MinBattlesForWinRate = 8;
    private const int Rows = 5;
    private const int DefaultWarEndMinuteUtc = 10 * 60;

    public async Task<ClanHqDto> ExecuteAsync(Clan clan, CancellationToken ct = default)
    {
        var key = $"hq:{clan.Id}";
        if (cache.TryGetValue(key, out ClanHqDto? cached) && cached is not null) return cached;

        Dictionary<string, ClanMemberInfo> members;
        try { members = await crApi.GetClanMembersAsync(clan.ClanTag, ct); }
        catch { members = new(StringComparer.OrdinalIgnoreCase); }

        var weeks = await WeeksAsync(clan, ct);
        var battles = await BattlesAsync(clan, ct);
        var players = Analyze(weeks, members, battles);

        var result = new ClanHqDto(
            WeeksAnalyzed: weeks.Count,
            ClanFamePerDeck: players.Median,
            Attention: players.Attention,
            Pillars: players.Pillars,
            Excluded: players.Excluded,
            Discipline: await discipline.ExecuteAsync(clan, ct));

        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        });
        return result;
    }

    /// <summary>Итог недели игрока.</summary>
    internal record WeekRow(string Name, int Fame, int Decks);

    /// <summary>Бои игрока в войне: сколько, сколько побед, сколько в последний час.</summary>
    internal record BattleRow(int Total, int Wins, int LastMinute);

    internal record Analysis(int Median, List<HqPlayerDto> Attention, List<HqPlayerDto> Pillars, int Excluded);

    /// <summary>
    /// Разбор по неделям (новые первыми). Вынесен отдельно от загрузки, чтобы правила
    /// «кто реальный» и «кто худший» проверялись тестами без базы и API.
    /// </summary>
    internal static Analysis Analyze(
        List<Dictionary<string, WeekRow>> weeks,
        Dictionary<string, ClanMemberInfo> members,
        Dictionary<string, BattleRow> battles)
    {
        var tags = members.Count > 0
            ? members.Keys.ToList()
            : weeks.SelectMany(w => w.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var real = new List<HqPlayerDto>();
        var excluded = 0;
        foreach (var tag in tags)
        {
            var mine = weeks.Select(w => w.GetValueOrDefault(tag)).ToList();
            var present = mine.Where(r => r is not null).Select(r => r!).ToList();
            var played = present.Where(r => r.Decks > 0).ToList();
            var decks = played.Sum(r => r.Decks);
            if (played.Count < MinWeeksPlayed || decks < MinDecks)
            {
                if (present.Count > 0) excluded++;
                continue;
            }

            var last = mine.FirstOrDefault();
            battles.TryGetValue(tag, out var b);
            members.TryGetValue(tag, out var member);
            real.Add(new HqPlayerDto(
                Tag: tag,
                Name: present[0].Name,
                Role: member?.Role,
                Weeks: present.Count,
                Decks: decks,
                DecksPossible: present.Count * WarDecksPerWeek,
                FamePerDeck: (int)Math.Round((double)played.Sum(r => r.Fame) / decks),
                LastWeekFamePerDeck: last is { Decks: > 0 } ? (int)Math.Round((double)last.Fame / last.Decks) : 0,
                WinRate: b is { Total: >= MinBattlesForWinRate } ? (int)Math.Round(100.0 * b.Wins / b.Total) : null,
                LastMinute: b?.LastMinute ?? 0,
                Reasons: []));
        }

        if (real.Count == 0) return new Analysis(0, [], [], excluded);

        var sorted = real.Select(r => r.FamePerDeck).OrderBy(v => v).ToList();
        var median = sorted[sorted.Count / 2];

        var scored = real.Select(r =>
        {
            var reasons = new List<string>();
            var deckRate = (double)r.Decks / r.DecksPossible;
            if (r.FamePerDeck < median * 0.85) reasons.Add("low_fame");
            if (r.WinRate is < 45) reasons.Add("losing");
            if (deckRate < 0.8) reasons.Add("skips");
            if (r.LastMinute >= 2) reasons.Add("late");
            if (r.LastWeekFamePerDeck > 0 && r.LastWeekFamePerDeck < r.FamePerDeck * 0.85) reasons.Add("falling");

            // Медали за колоду весят больше всего: это и есть «как играет войну».
            // Пропуски - вторые: не сыгранная колода теряет клану больше проигранной.
            var score = Math.Max(0, 1 - (double)r.FamePerDeck / Math.Max(1, median)) * 100
                        + (1 - Math.Min(1, deckRate)) * 80
                        + (r.WinRate is { } w && w < 50 ? (50 - w) * 0.8 : 0)
                        + Math.Min(r.LastMinute, 6) * 1.5;
            return (Row: r with { Reasons = reasons }, Score: score);
        }).ToList();

        var attention = scored
            .Where(x => x.Row.Reasons.Count > 0 && x.Score >= 10)
            .OrderByDescending(x => x.Score)
            .Take(Rows)
            .Select(x => x.Row)
            .ToList();
        var flagged = attention.Select(a => a.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pillars = scored
            .Where(x => !flagged.Contains(x.Row.Tag) && (double)x.Row.Decks / x.Row.DecksPossible >= 0.9)
            .OrderByDescending(x => x.Row.FamePerDeck)
            .ThenByDescending(x => x.Row.Decks)
            .Take(3)
            .Select(x => x.Row)
            .ToList();

        return new Analysis(median, attention, pillars, excluded);
    }

    /// <summary>
    /// Завершённые недели, новые первыми: свои снимки, а где их нет - журнал войн.
    /// Текущая неделя не идёт: она не доиграна, и в худших оказался бы любой.
    /// </summary>
    private async Task<List<Dictionary<string, WeekRow>>> WeeksAsync(Clan clan, CancellationToken ct)
    {
        var byWeek = new Dictionary<(int, int), Dictionary<string, WeekRow>>();
        foreach (var week in (await snapshots.GetByClanAsync(clan.Id, WeeksWindow + 2, ct)).GroupBy(s => (s.SeasonId, s.SectionIndex)))
        {
            var final = week.OrderByDescending(s => s.PeriodIndex).First();
            byWeek[week.Key] = final.Players
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new WeekRow(g.First().Name, g.First().Fame, g.First().DecksUsed),
                    StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            foreach (var w in await crApi.GetRiverRaceLogAsync(clan.ClanTag, ct))
            {
                if (byWeek.ContainsKey((w.SeasonId, w.SectionIndex))) continue;
                var ours = w.Standings.FirstOrDefault(s => string.Equals(s.ClanTag, clan.ClanTag, StringComparison.OrdinalIgnoreCase));
                if (ours is null) continue;
                byWeek[(w.SeasonId, w.SectionIndex)] = ours.Participants
                    .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => new WeekRow(g.First().Name, g.First().Fame, g.First().DecksUsed),
                        StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { /* журнал не критичен */ }

        try
        {
            if (await crApi.GetCurrentWarAsync(clan.ClanTag, ct) is { } war) byWeek.Remove((war.SeasonId, war.SectionIndex));
        }
        catch { /* текущую неделю не узнали - считаем по всем */ }

        return byWeek
            .OrderByDescending(kv => kv.Key.Item1).ThenByDescending(kv => kv.Key.Item2)
            .Take(WeeksWindow)
            .Select(kv => kv.Value)
            .ToList();
    }

    private async Task<Dictionary<string, BattleRow>> BattlesAsync(Clan clan, CancellationToken ct)
    {
        var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
        var endMinute = settings.WarEndMinuteUtc is int m && m >= 0 && m < 1440 ? m : DefaultWarEndMinuteUtc;

        List<WarBattle> list;
        try { list = await warBattles.GetSinceAsync(clan.Id, DateTime.UtcNow.AddDays(-7 * WeeksWindow), ct); }
        catch { return new(StringComparer.OrdinalIgnoreCase); }

        return list
            .GroupBy(b => b.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new BattleRow(
                Total: g.Count(),
                Wins: g.Count(b => b.Won),
                LastMinute: g.Count(b =>
                {
                    var end = new DateTime(b.BattleTimeUtc.Year, b.BattleTimeUtc.Month, b.BattleTimeUtc.Day,
                        endMinute / 60, endMinute % 60, 0, DateTimeKind.Utc);
                    if (end <= b.BattleTimeUtc) end = end.AddDays(1);
                    return (end - b.BattleTimeUtc).TotalHours <= 1;
                })), StringComparer.OrdinalIgnoreCase);
    }
}
