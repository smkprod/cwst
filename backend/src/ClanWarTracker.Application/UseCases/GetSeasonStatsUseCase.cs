using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Сезонный зачёт: агрегирует финальные снимки каждой недели сезона.
/// Слава в снимке накопительная за неделю, поэтому берём последний день недели
/// и суммируем по неделям — получаем «сколько игрок набил за сезон».
/// </summary>
public class GetSeasonStatsUseCase(IWarSnapshotRepository snapshots)
{
    /// <param name="seasonId">null — последний сезон с данными.</param>
    /// <returns>null — данных ещё нет совсем.</returns>
    public async Task<SeasonStatsDto?> ExecuteAsync(int clanId, int? seasonId = null, CancellationToken ct = default)
    {
        seasonId ??= await snapshots.GetLatestSeasonIdAsync(clanId, ct);
        if (seasonId is null) return null;

        var all = await snapshots.GetBySeasonAsync(clanId, seasonId.Value, ct);
        if (all.Count == 0) return new SeasonStatsDto(seasonId.Value, 0, []);

        // Финальный снимок каждой недели (максимальный PeriodIndex)
        var weekFinals = all
            .GroupBy(s => s.SectionIndex)
            .Select(g => g.OrderBy(s => s.PeriodIndex).Last())
            .ToList();

        var agg = new Dictionary<string, (string Name, int Total, int Weeks, int Best)>(StringComparer.OrdinalIgnoreCase);
        foreach (var week in weekFinals)
        {
            foreach (var p in week.Players)
            {
                agg.TryGetValue(p.PlayerTag, out var cur); // (null, 0, 0, 0) если впервые
                agg[p.PlayerTag] = (
                    p.Name, // актуальное имя из последнего снимка, где игрок встречался
                    cur.Total + p.Fame,
                    cur.Weeks + (p.Fame > 0 ? 1 : 0),
                    Math.Max(cur.Best, p.Fame));
            }
        }

        var players = agg
            .OrderByDescending(kv => kv.Value.Total)
            .Select((kv, i) => new SeasonPlayerDto(
                PlayerTag: kv.Key,
                Name: kv.Value.Name,
                TotalFame: kv.Value.Total,
                WeeksParticipated: kv.Value.Weeks,
                BestWeekFame: kv.Value.Best,
                Rank: i + 1))
            .ToList();

        return new SeasonStatsDto(seasonId.Value, weekFinals.Count, players);
    }
}
