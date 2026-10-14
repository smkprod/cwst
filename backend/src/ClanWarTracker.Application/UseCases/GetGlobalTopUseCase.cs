using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Глобальный топ среди игроков, привязавших аккаунт к боту (/link), по всем кланам сервиса.
/// Слава суммируется по финальным снимкам последних недель — как в истории игрока.
/// </summary>
public class GetGlobalTopUseCase(IPlayerRepository players, IWarSnapshotRepository snapshots, IMemoryCache cache)
{
    private const int WeeksWindow = 8;

    /// <summary>
    /// Таблица общая для всех, кроме отметки «это я», - считаем её раз в пять минут, а не
    /// на каждое открытие: под ней вся военная история всех привязанных игроков.
    /// </summary>
    public async Task<GlobalTopDto> ExecuteAsync(long? meTelegramId = null, CancellationToken ct = default)
    {
        var table = await cache.GetOrCreateAsync("globaltop", async entry =>
        {
            entry.Size = 50;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await BuildAsync(ct);
        }) ?? [];
        var rows = table
            .Select(r => r.Row with { IsMe = meTelegramId is not null && r.Tg == meTelegramId })
            .ToList();
        return new GlobalTopDto(WeeksWindow, rows.Count, rows);
    }

    private async Task<List<(GlobalTopPlayerDto Row, long? Tg)>> BuildAsync(CancellationToken ct)
    {
        var linked = await players.GetAllLinkedAsync(ct);
        if (linked.Count == 0) return [];

        var tags = linked.Select(p => p.PlayerTag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rows = await snapshots.GetPlayersHistoryAsync(tags, WeeksWindow, ct);

        var historyByTag = rows
            .GroupBy(r => r.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Один тег мог быть привязан из нескольких групп — берём первую запись
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<(GlobalTopPlayerDto Row, long? Tg)>();
        foreach (var p in linked)
        {
            if (!seen.Add(p.PlayerTag)) continue;

            var hist = historyByTag.GetValueOrDefault(p.PlayerTag) ?? [];
            var totalFame = hist.Sum(h => h.Fame);
            var totalDecks = hist.Sum(h => h.DecksUsed);

            items.Add((new GlobalTopPlayerDto(
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
                IsMe: false), p.TelegramUserId));
        }

        return items
            .OrderByDescending(i => i.Row.TotalFame)
            .ThenByDescending(i => i.Row.WeeksParticipated)
            .Select((i, idx) => (i.Row with { Rank = idx + 1 }, i.Tg))
            .ToList();
    }
}
