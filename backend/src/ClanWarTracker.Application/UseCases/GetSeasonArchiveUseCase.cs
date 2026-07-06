using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Архив лучших игроков по сезонам, новые — первыми. Включаем и текущий сезон (у молодых
/// кланов он часто единственный) — иначе архив был бы пустым. По каждому сезону берём
/// топ-игроков из того же агрегата, что и сезонный зачёт (сумма финальной славы по неделям).
/// </summary>
public class GetSeasonArchiveUseCase(
    IWarSnapshotRepository snapshots,
    GetSeasonStatsUseCase seasonStats)
{
    private const int MaxSeasons = 8;   // не тянем всю историю разом
    private const int TopPerSeason = 10;

    public async Task<SeasonArchiveDto> ExecuteAsync(int clanId, CancellationToken ct = default)
    {
        var ids = await snapshots.GetSeasonIdsAsync(clanId, ct);
        if (ids.Count == 0) return new SeasonArchiveDto([]);

        var seasonIds = ids.Take(MaxSeasons).ToList();

        var entries = new List<SeasonArchiveEntryDto>();
        foreach (var id in seasonIds)
        {
            var s = await seasonStats.ExecuteAsync(clanId, id, ct);
            if (s is null || s.Players.Count == 0) continue;

            entries.Add(new SeasonArchiveEntryDto(
                SeasonId: s.SeasonId,
                WeeksTracked: s.WeeksTracked,
                ClanTotalFame: s.Players.Sum(p => p.TotalFame),
                TopPlayers: s.Players.Take(TopPerSeason).ToList()));
        }

        return new SeasonArchiveDto(entries);
    }
}
