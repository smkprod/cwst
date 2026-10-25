using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Разведка+» (Клан Про): соперники изнутри.
///
/// Обычная разведка говорит, чего клан стоит по журналу. Здесь - из чего он состоит и
/// как живёт военный день: кто у них приносит медали, сколько эти люди берут за колоду,
/// во сколько клан садится играть и когда к нему приходят очки. Из этого глава решает
/// простые вещи: когда поднимать своих, чтобы не отстать к вечеру, и стоит ли догонять.
///
/// Ход дня собирает воркер (<see cref="CaptureWarSnapshotsUseCase"/>) раз в полчаса,
/// только для кланов с Клан Про. Состав и медали берутся из живой гонки.
/// </summary>
public class GetScoutPlusUseCase(IClashRoyaleApi crApi, IRaceTickRepository raceTicks, IMemoryCache cache)
{
    private const int TopPlayers = 5;

    /// <summary>Сколько недель хода дня берём в расчёт времени.</summary>
    private static readonly TimeSpan TimingWindow = TimeSpan.FromDays(28);

    public async Task<ScoutPlusDto?> ExecuteAsync(string clanTag, CancellationToken ct = default)
    {
        var war = await crApi.GetCurrentWarAsync(clanTag, ct);
        if (war is null || war.RaceClans.Count == 0) return null;

        var now = DateTime.UtcNow;
        var dayStart = war.DayEndsAtUtc.AddHours(-24);
        var hoursInto = Math.Clamp((now - dayStart).TotalHours, 0, 24);
        var tags = war.RaceClans.Select(c => c.Tag).ToList();

        var ticks = await raceTicks.GetTicksAsync(tags, now - TimingWindow, ct);
        var days = war.IsWarDay
            ? await raceTicks.GetPlayerDaysAsync(tags, war.SeasonId, war.SectionIndex, ct)
            : [];

        var rows = war.RaceClans
            .OrderByDescending(c => c.Fame)
            .ThenByDescending(c => c.TodayFame)
            .Select(c =>
            {
                var clanTicks = ticks.Where(t => Same(t.ClanTag, c.Tag)).ToList();
                var clanDays = days.Where(d => Same(d.ClanTag, c.Tag)).ToList();
                return new ScoutPlusClanDto(
                    Tag: c.Tag,
                    Name: c.Name,
                    IsOurClan: Same(c.Tag, war.ClanTag),
                    Fame: c.Fame,
                    DecksToday: war.IsWarDay ? c.DecksUsedToday : 0,
                    ActiveToday: war.IsWarDay ? c.Players.Count(p => p.DecksUsedToday > 0) : 0,
                    Fighters: c.Players.Count(p => p.Fame > 0),
                    Top: Top(c, clanDays, war, dayStart),
                    Timing: Timing(clanTicks, dayStart, war.IsWarDay ? hoursInto : null,
                        war.IsWarDay ? c.DecksUsedToday : null));
            })
            .ToList();

        return new ScoutPlusDto(war.IsWarDay, war.PeriodIndex, dayStart, Math.Round(hoursInto, 2), rows);
    }

    /// <summary>
    /// Лучшие игроки клана по медалям за неделю. Колоды - из наших дневных снимков, если
    /// мы видели все прошедшие военные дни: в живой гонке CR смешивает с войной и
    /// тренировочные бои, и «медали за колоду» по ним занижены.
    /// </summary>
    private static List<ScoutPlusPlayerDto> Top(RaceClanStanding c, List<RacePlayerDay> clanDays, WarStatus war, DateTime dayStart)
    {
        var warDaysSoFar = war.IsWarDay ? Math.Clamp(war.PeriodIndex - 2, 1, 4) : 0;
        var seenDays = clanDays.Select(d => d.DayIndex).Distinct().Count();
        var exact = warDaysSoFar > 0 && seenDays >= warDaysSoFar;
        var byPlayer = clanDays.GroupBy(d => d.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return c.Players
            .Where(p => p.Fame > 0)
            .OrderByDescending(p => p.Fame)
            .Take(TopPlayers)
            .Select(p =>
            {
                var mine = byPlayer.GetValueOrDefault(p.Tag) ?? [];
                var decks = exact ? mine.Sum(d => d.Decks)
                    : war.PeriodIndex == 3 ? p.DecksUsedToday
                    : p.DecksUsed;
                decks = Math.Max(decks, 1);

                // Когда доигрывает: медиана последней колоды дня, в часах от начала его дня.
                // Начала прошлых дней считаем от текущего - смена дня у CR в одно и то же время.
                var hours = mine
                    .Where(d => d.LastPlayUtc is not null)
                    .Select(d => HoursIntoDay(d.LastPlayUtc!.Value, dayStart))
                    .OrderBy(h => h)
                    .ToList();

                return new ScoutPlusPlayerDto(
                    Tag: p.Tag,
                    Name: p.Name,
                    Fame: p.Fame,
                    Decks: decks,
                    FamePerDeck: (int)Math.Round((double)p.Fame / decks),
                    DaysPlayed: mine.Count(d => d.Decks > 0),
                    DaysObserved: seenDays,
                    UsualHour: hours.Count > 0 ? Math.Round(hours[hours.Count / 2], 1) : null);
            })
            .ToList();
    }

    /// <summary>
    /// Распорядок дня клана по получасовым снимкам: сколько колод и медалей приходит в
    /// каждый час дня, к какому часу сыграна половина и почти всё. Дни без утра или без
    /// вечера в доли «к какому часу» не берём: без начала или конца дня они бы врали.
    /// </summary>
    internal static ScoutTimingDto? Timing(List<RaceTick> ticks, DateTime dayStart, double? hoursNow, int? decksNow)
    {
        var hourDecks = new double[24];
        var hourFame = new double[24];
        var half = new List<double>();
        var most = new List<double>();
        var atNow = new List<double>();
        var finals = new List<int>();
        var observed = 0;

        foreach (var day in ticks.GroupBy(t => (t.SeasonId, t.SectionIndex, t.DayIndex)))
        {
            var list = day.OrderBy(t => t.SlotUtc).ToList();
            if (list.Count < 2) continue;
            observed++;

            for (var i = 1; i < list.Count; i++)
            {
                var a = list[i - 1];
                var b = list[i];
                if ((b.SlotUtc - a.SlotUtc).TotalHours > 1.5) continue; // дыра - распределить нечестно
                var h = (int)Math.Floor(HoursIntoDay(b.SlotUtc, dayStart)) % 24;
                hourDecks[h] += Math.Max(0, b.DecksUsedToday - a.DecksUsedToday);
                hourFame[h] += Math.Max(0, b.Fame - a.Fame);
            }

            var first = HoursIntoDay(list[0].SlotUtc, dayStart);
            var last = HoursIntoDay(list[^1].SlotUtc, dayStart);
            var final = list[^1].DecksUsedToday;
            if (first > 1.5 || last < 22.5 || final < 10) continue;

            finals.Add(final);
            half.Add(FirstReach(list, final * 0.5, dayStart));
            most.Add(FirstReach(list, final * 0.9, dayStart));
            if (hoursNow is { } hn)
            {
                var before = list.LastOrDefault(t => HoursIntoDay(t.SlotUtc, dayStart) <= hn);
                atNow.Add(before is null ? 0 : (double)before.DecksUsedToday / final);
            }
        }

        var totalDecks = hourDecks.Sum();
        if (observed == 0 || totalDecks <= 0) return null;
        var totalFame = hourFame.Sum();

        var peak = 0;
        var peakFame = -1.0;
        for (var h = 0; h < 24; h++)
        {
            var w = hourFame[h] + hourFame[(h + 1) % 24];
            if (w > peakFame) { peakFame = w; peak = h; }
        }

        return new ScoutTimingDto(
            DaysObserved: observed,
            HourDecks: Percents(hourDecks, totalDecks),
            HourFame: totalFame > 0 ? Percents(hourFame, totalFame) : Enumerable.Repeat(0, 24).ToList(),
            PeakHour: peak,
            PeakSharePercent: totalFame > 0 ? (int)Math.Round(peakFame / totalFame * 100) : 0,
            HalfDoneHour: half.Count > 0 ? Math.Round(half.Average(), 1) : null,
            MostlyDoneHour: most.Count > 0 ? Math.Round(most.Average(), 1) : null,
            UsualPercentNow: atNow.Count > 0 ? (int)Math.Round(atNow.Average() * 100) : null,
            TodayPercentNow: decksNow is { } dn && finals.Count > 0
                ? (int)Math.Round(dn / finals.Average() * 100)
                : null);
    }

    /// <summary>Час дня, к которому колод стало не меньше порога.</summary>
    private static double FirstReach(List<RaceTick> day, double threshold, DateTime dayStart)
    {
        var hit = day.FirstOrDefault(t => t.DecksUsedToday >= threshold) ?? day[^1];
        return HoursIntoDay(hit.SlotUtc, dayStart);
    }

    /// <summary>Часы от начала военного дня, к которому относится момент: 0..24.</summary>
    internal static double HoursIntoDay(DateTime utc, DateTime dayStart)
    {
        var h = (utc - dayStart).TotalHours % 24;
        return h < 0 ? h + 24 : h;
    }

    private static List<int> Percents(double[] values, double total) =>
        values.Select(v => (int)Math.Round(v / total * 100)).ToList();

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Как играет игрок соперника: победы в войне и его военные колоды по журналу боёв.
    /// Только для игроков текущей гонки клана - ручка не должна стать прокси к CR API.
    /// Кэш полчаса: журнал меняется, но разведке хватит и такой свежести.
    /// </summary>
    public async Task<ScoutPlayerPlayDto?> PlayerAsync(string clanTag, string playerTag, CancellationToken ct = default)
    {
        var war = await crApi.GetCurrentWarAsync(clanTag, ct);
        var player = war?.RaceClans.SelectMany(c => c.Players).FirstOrDefault(p => Same(p.Tag, playerTag));
        if (player is null) return null;

        var key = $"scoutplay:{player.Tag}";
        if (cache.TryGetValue(key, out ScoutPlayerPlayDto? cached) && cached is not null) return cached;

        var battles = (await crApi.GetRecentBattlesAsync(player.Tag, ct))
            .Where(b => b.Type.StartsWith("riverRace", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var decks = battles
            .Where(b => b.MyDeck.Count == 8)
            .GroupBy(b => string.Join("|", b.MyDeck.Select(c => c.Name).OrderBy(n => n)))
            .OrderByDescending(g => g.Count())
            .Take(4)
            .Select(g => new ScoutDeckDto(
                Games: g.Count(),
                Wins: g.Count(b => b.Won),
                Cards: g.First().MyDeck
                    .Select(c => new ScoutCardDto(c.Name, c.EvolutionLevel > 0 && c.EvoIconUrl is { } evo ? evo : c.IconUrl,
                        c.Level, c.EvolutionLevel > 0))
                    .ToList()))
            .ToList();

        var result = new ScoutPlayerPlayDto(
            Tag: player.Tag,
            Name: player.Name,
            WarWins: battles.Count(b => b.Won),
            WarLosses: battles.Count(b => !b.Won),
            Decks: decks);

        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            Size = 5,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        });
        return result;
    }
}
