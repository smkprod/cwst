using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class GetGlobalTopUseCase(IPlayerRepository players, IWarSnapshotRepository snapshots)
{
    private const int WeeksWindow = 8;

    public async Task<GlobalTopDto> ExecuteAsync(long? callerTelegramUserId, CancellationToken ct = default)
    {
        var linked = await players.GetAllLinkedAsync(ct);
        if (linked.Count == 0)
            return new GlobalTopDto(WeeksWindow, 0, []);

        var tags = linked.Select(p => p.PlayerTag).ToHashSet();
        var history = await snapshots.GetPlayersHistoryAsync(tags, WeeksWindow, ct);

        // Determine the global week cutoff: N most recent distinct (SeasonId, SectionIndex) pairs
        var weekKeys = history
            .Select(r => (r.Snapshot!.SeasonId, r.Snapshot.SectionIndex))
            .Distinct()
            .OrderByDescending(k => k.SeasonId).ThenByDescending(k => k.SectionIndex)
            .Take(WeeksWindow)
            .ToHashSet();

        var inWindow = history.Where(r =>
            weekKeys.Contains((r.Snapshot!.SeasonId, r.Snapshot.SectionIndex))).ToList();

        var byPlayer = inWindow.GroupBy(r => r.PlayerTag).ToList();

        var playerLookup = linked.ToDictionary(p => p.PlayerTag);
        var callerTag = callerTelegramUserId.HasValue
            ? linked.FirstOrDefault(p => p.TelegramUserId == callerTelegramUserId)?.PlayerTag
            : null;

        var ranked = byPlayer
            .Select(g =>
            {
                var totalFame = g.Sum(r => r.Fame);
                var totalDecks = g.Sum(r => r.DecksUsed);
                var bestWeek = g.Max(r => r.Fame);
                var avg = totalDecks > 0 ? Math.Round((double)totalFame / totalDecks, 1) : 0.0;
                var player = playerLookup.GetValueOrDefault(g.Key);
                return new
                {
                    PlayerTag = g.Key,
                    Name = player?.Name ?? g.Key,
                    ClanName = player?.Clan?.Name ?? "—",
                    TotalFame = totalFame,
                    WeeksParticipated = g.Count(),
                    BestWeekFame = bestWeek,
                    AvgFamePerAttack = avg,
                };
            })
            .OrderByDescending(x => x.TotalFame)
            .ToList();

        var dtos = ranked.Select((x, i) => new GlobalTopPlayerDto(
            PlayerTag: x.PlayerTag,
            Name: x.Name,
            ClanName: x.ClanName,
            TotalFame: x.TotalFame,
            WeeksParticipated: x.WeeksParticipated,
            BestWeekFame: x.BestWeekFame,
            AvgFamePerAttack: x.AvgFamePerAttack,
            Rank: i + 1,
            IsMe: x.PlayerTag == callerTag)).ToList();

        return new GlobalTopDto(WeeksWindow, dtos.Count, dtos);
    }
}
