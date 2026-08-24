using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Разведка гонки»: досье на каждый клан текущей недели, собранное из его же
/// публичного журнала войн.
///
/// Таблица гонки отвечает на вопрос «кто впереди сейчас». Это вопрос сегодняшнего дня,
/// и к четвергу он бесполезен: клан, который вырвался в среду, может оказаться слабее
/// того, кто раскачивается к воскресенью. Разведка отвечает на другой вопрос — чего
/// эти кланы стоят вообще: какой у них обычный результат, насколько они стабильны,
/// доигрывают ли атаки и идут ли сейчас выше или ниже себя самих.
///
/// Из этого следует решение, ради которого всё и затевалось: упираться в эту неделю
/// или второе место — уже потолок.
/// </summary>
public class GetRaceScoutUseCase(IClashRoyaleApi crApi, IMemoryCache cache)
{
    /// <summary>Сколько завершённых недель соперника берём в расчёт.</summary>
    private const int WeeksWindow = 8;

    /// <summary>Военных колод за неделю: 4 дня × 4 колоды.</summary>
    private const int WarDecksPerWeek = 16;

    /// <summary>Ниже этого темпа относительно своего обычного соперник «просел».</summary>
    private const int BelowUsualThreshold = -15;

    /// <summary>Разброс результатов, с которого клан считается непредсказуемым.</summary>
    private const int UnstableThreshold = 50;

    public async Task<RaceScoutDto?> ExecuteAsync(string clanTag, bool isPro, CancellationToken ct = default)
    {
        var war = await crApi.GetCurrentWarAsync(clanTag, ct);
        if (war is null) return null;

        // Порядок мест — тот же, что в таблице гонки, чтобы разведка читалась рядом с ней.
        var standings = war.RaceClans
            .OrderByDescending(c => c.Fame)
            .ThenByDescending(c => c.TodayFame)
            .ToList();
        if (standings.Count == 0) return null;

        // Доля недели, которая уже прошла: по ней считаем, идёт ли клан выше своего обычного.
        // Военных дней четыре (periodIndex 3..6), текущий засчитываем частично.
        var dayNumber = Math.Clamp(war.PeriodIndex - 2, 0, 4);
        var hoursLeft = Math.Max(0, (war.DayEndsAtUtc - DateTime.UtcNow).TotalHours);
        var elapsed = war.IsWarDay
            ? Math.Clamp(dayNumber - 1 + (24 - Math.Min(24, hoursLeft)) / 24.0, 0.05, 4)
            : 0;

        var rows = new List<ScoutClanDto>();
        foreach (var (c, i) in standings.Select((c, i) => (c, i)))
        {
            var history = await HistoryAsync(c.Tag, ct);

            // Темп считаем, только когда есть с чем сравнивать: без истории соперника
            // и без начавшейся войны любое число здесь было бы выдумкой.
            var pace = history.AvgWeekFame > 0 && elapsed > 0
                ? (int)Math.Round((c.Fame / (history.AvgWeekFame * elapsed / 4.0) - 1) * 100)
                : 0;

            rows.Add(new ScoutClanDto(
                Tag: c.Tag,
                Name: c.Name,
                Position: i + 1,
                IsOurClan: string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase),
                CurrentFame: c.Fame,
                DayPoints: c.DayPoints,
                WeeksTracked: history.Weeks,
                AvgWeekFame: history.AvgWeekFame,
                BestWeekFame: history.BestWeekFame,
                AvgRank: history.AvgRank,
                Volatility: history.Volatility,
                AvgDecksPerPlayer: history.AvgDecksPerPlayer,
                AvgParticipants: history.AvgParticipants,
                PaceVsUsualPercent: Math.Clamp(pace, -99, 999),
                FadesLate: FadesLate(c.DayPoints)));
        }

        var weeksAnalyzed = rows.Count == 0 ? 0 : rows.Max(r => r.WeeksTracked);
        var rivals = rows.Where(r => !r.IsOurClan).ToList();

        // Настоящий соперник — не тот, кто впереди сегодня, а тот, кто сильнее обычно.
        // Ровно в этом расхождении и польза: сегодняшний лидер часто просто выстрелил.
        var realRival = rivals
            .Where(r => r.WeeksTracked > 0)
            .OrderByDescending(r => r.AvgWeekFame)
            .FirstOrDefault();

        return new RaceScoutDto(
            IsPro: isPro,
            WeeksAnalyzed: weeksAnalyzed,
            // На Free отдаём только то, что и так видно в таблице гонки: имена и места.
            // Цифры разведки — платные, и прятать их надо на сервере, а не в интерфейсе.
            Clans: isPro ? rows : rows.Select(Redact).ToList(),
            FreeTeaser: isPro ? null : Teaser(rows, rivals),
            RealRivalTag: isPro ? realRival?.Tag : null);
    }

    /// <summary>Оставляет только публичное: имя, место и текущие медали.</summary>
    private static ScoutClanDto Redact(ScoutClanDto c) => c with
    {
        DayPoints = [],
        WeeksTracked = 0,
        AvgWeekFame = 0,
        BestWeekFame = 0,
        AvgRank = 0,
        Volatility = 0,
        AvgDecksPerPlayer = 0,
        AvgParticipants = 0,
        PaceVsUsualPercent = 0,
        FadesLate = false,
    };

    /// <summary>
    /// Код дразнилки для Free. Обещаем ровно то, что реально посчитано, — иначе
    /// человек купит Pro и не найдёт внутри того, что ему показали.
    /// null — сказать нечего, и тогда честнее промолчать.
    /// </summary>
    private static string? Teaser(List<ScoutClanDto> all, List<ScoutClanDto> rivals)
    {
        if (rivals.Any(r => r.WeeksTracked > 0 && r.PaceVsUsualPercent <= BelowUsualThreshold))
            return "rivalBelowUsual";
        if (rivals.Any(r => r.WeeksTracked >= 3 && r.Volatility >= UnstableThreshold))
            return "rivalUnstable";

        var ours = all.FirstOrDefault(r => r.IsOurClan);
        var best = rivals.Where(r => r.WeeksTracked > 0).OrderByDescending(r => r.AvgWeekFame).FirstOrDefault();
        if (ours is { WeeksTracked: > 0 } && best is not null && ours.AvgWeekFame > best.AvgWeekFame)
            return "weAreStronger";

        return rivals.Any(r => r.WeeksTracked > 0) ? "generic" : null;
    }

    /// <summary>
    /// Просел ли клан во второй половине недели: последний завершённый день заметно
    /// слабее среднего по предыдущим. Меньше трёх дней — судить не о чем.
    /// </summary>
    private static bool FadesLate(List<int> dayPoints)
    {
        if (dayPoints.Count < 3) return false;
        var earlier = dayPoints.Take(dayPoints.Count - 1).ToList();
        var avgEarlier = earlier.Average();
        return avgEarlier > 0 && dayPoints[^1] < avgEarlier * 0.8;
    }

    private record ClanHistory(
        int Weeks, int AvgWeekFame, int BestWeekFame, double AvgRank,
        int Volatility, double AvgDecksPerPlayer, int AvgParticipants);

    private static readonly ClanHistory Empty = new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Досье по журналу войн клана. Кэш на час: журнал меняется раз в неделю, а запрос
    /// уходит на каждый клан гонки — без кэша открытие экрана стоило бы пяти обращений
    /// к CR API, и так у каждого лидера, у которого открыто приложение.
    /// </summary>
    private async Task<ClanHistory> HistoryAsync(string clanTag, CancellationToken ct)
    {
        var key = $"scout:{clanTag}";
        if (cache.TryGetValue(key, out ClanHistory? cached) && cached is not null) return cached;

        List<RiverRaceLogWeek> log;
        try { log = await crApi.GetRiverRaceLogAsync(clanTag, ct); }
        catch { return Empty; }   // соперник недоступен — это не повод не показать остальных

        var mine = log
            .Select(w => w.Standings.FirstOrDefault(s =>
                string.Equals(s.ClanTag, clanTag, StringComparison.OrdinalIgnoreCase)))
            .Where(s => s is not null)
            .Select(s => s!)
            .Take(WeeksWindow)
            .ToList();
        if (mine.Count == 0) return Empty;

        var fames = mine.Select(s => (double)s.Fame).ToList();
        var mean = fames.Average();
        var std = fames.Count > 1
            ? Math.Sqrt(fames.Sum(f => (f - mean) * (f - mean)) / fames.Count)
            : 0;

        // Считаем только тех, кто реально воевал: нулевые участники — это состав,
        // а не бойцы, и они занижали бы среднее число колод у любого клана.
        var fighters = mine
            .SelectMany(s => s.Participants.Where(p => p.DecksUsed > 0))
            .ToList();
        var weeksWithRoster = mine.Count(s => s.Participants.Any(p => p.DecksUsed > 0));

        var result = new ClanHistory(
            Weeks: mine.Count,
            AvgWeekFame: (int)Math.Round(mean),
            BestWeekFame: mine.Max(s => s.Fame),
            AvgRank: Math.Round(mine.Average(s => s.Rank), 1),
            Volatility: mean > 0 ? Math.Clamp((int)Math.Round(std / mean * 100), 0, 100) : 0,
            AvgDecksPerPlayer: fighters.Count > 0
                ? Math.Round(Math.Min(WarDecksPerWeek, fighters.Average(p => p.DecksUsed)), 1)
                : 0,
            AvgParticipants: weeksWithRoster > 0
                ? (int)Math.Round((double)fighters.Count / weeksWithRoster)
                : 0);

        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        });
        return result;
    }
}
