using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Глобальный топ среди игроков, привязавших аккаунт к боту (/link), по всем кланам сервиса.
/// Слава суммируется по финальным снимкам последних недель — как в истории игрока.
/// </summary>
public class GetGlobalTopUseCase(IPlayerRepository players, IWarSnapshotRepository snapshots)
{
    private const int WeeksWindow = 8;

    public async Task<GlobalTopDto> ExecuteAsync(long? meTelegramId = null, CancellationToken ct = default)
    {
        var linked = await players.GetAllLinkedAsync(ct);
        if (linked.Count == 0) return new GlobalTopDto(WeeksWindow, 0, []);

        var tags = linked.Select(p => p.PlayerTag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rows = await snapshots.GetPlayersHistoryAsync(tags, WeeksWindow, ct);

        var historyByTag = rows
            .GroupBy(r => r.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Один тег мог быть привязан из нескольких групп — берём первую запись
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<GlobalTopPlayerDto>();
        foreach (var p in linked)
        {
            if (!seen.Add(p.PlayerTag)) continue;

            var hist = historyByTag.GetValueOrDefault(p.PlayerTag) ?? [];
            var totalFame = hist.Sum(h => h.Fame);
            var totalDecks = hist.Sum(h => h.DecksUsed);

            items.Add(new GlobalTopPlayerDto(
                PlayerTag: p.PlayerTag,
                Name: p.Name,
                ClanName: p.Clan?.Name ?? "—",
                TotalFame: totalFame,
                WeeksParticipated: hist.Count(h => h.Fame > 0),
                BestWeekFame: hist.Count > 0 ? hist.Max(h => h.Fame) : 0,
                AvgFamePerAttack: totalFame > 0 && totalDecks > 0
                    ? Math.Round(Math.Clamp((double)totalFame / totalDecks, 100, 250), 1)
                    : 0,
                Rank: 0,
                IsMe: meTelegramId is not null && p.TelegramUserId == meTelegramId));
        }

        var ranked = items
            .OrderByDescending(i => i.TotalFame)
            .ThenByDescending(i => i.WeeksParticipated)
            .Select((i, idx) => i with { Rank = idx + 1 })
            .ToList();

        return new GlobalTopDto(WeeksWindow, ranked.Count, ranked);
    }
}
