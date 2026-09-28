using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Бои топа за неделю в памяти: колоды в виде отсортированных id карт, чтобы
/// статистика матчапа считалась за миллисекунды на каждое открытие разбора.
///
/// Перечитывается, как и <see cref="MetaWindow"/>: появился новый день или прошла
/// четверть часа.
/// </summary>
public static class MatchupWindow
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Data? _cached;
    private static DateTime _loadedAtUtc;

    /// <param name="Cards">По 16 id на бой: 8 карт стороны A, потом 8 карт B, каждая восьмёрка по возрастанию.</param>
    /// <param name="Results">Итог для стороны A: 1, 0, −1.</param>
    public sealed record Data(string ToDayUtc, int[] Cards, sbyte[] Results)
    {
        public int Count => Results.Length;
    }

    /// <returns>null - боёв топа ещё нет (их начали хранить не сразу).</returns>
    public static async Task<Data?> LoadAsync(IMetaRepository meta, CancellationToken ct)
    {
        var latest = await meta.LatestDayAsync(ct);
        if (latest is null) return null;

        var cached = _cached;
        if (Fresh(cached, latest)) return cached!.Count == 0 ? null : cached;

        await Gate.WaitAsync(ct);
        try
        {
            cached = _cached;
            if (!Fresh(cached, latest))
            {
                var from = DateOnly.Parse(latest).AddDays(-(MetaWindow.Days - 1)).ToString("yyyy-MM-dd");
                var rows = await meta.GetBattlesSinceAsync(from, ct);
                var cards = new List<int>(rows.Count * 16);
                var results = new List<sbyte>(rows.Count);
                foreach (var r in rows)
                {
                    var a = Ids(r.DeckA);
                    var b = Ids(r.DeckB);
                    if (a.Length != 8 || b.Length != 8) continue;
                    cards.AddRange(a);
                    cards.AddRange(b);
                    results.Add((sbyte)Math.Sign(r.Result));
                }
                cached = new Data(latest, cards.ToArray(), results.ToArray());
                _cached = cached;
                _loadedAtUtc = DateTime.UtcNow;
            }
            return cached.Count == 0 ? null : cached;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Пустую неделю перепроверяем через минуту, а не через четверть часа: снимок
    /// за тот же день, собранный из панели, иначе был бы не виден до 15 минут, и
    /// выглядело бы так, будто сбор ничего не дал.
    /// </summary>
    private static bool Fresh(Data? cached, string latest) =>
        cached is not null && cached.ToDayUtc == latest
        && DateTime.UtcNow - _loadedAtUtc < (cached.Count == 0 ? TimeSpan.FromMinutes(1) : Ttl);

    /// <summary>Карты колоды без различия эволюции, по возрастанию: так сравнивать дёшево.</summary>
    public static int[] Ids(string deckKey) =>
        MetaCard.ParseDeckKey(deckKey).Select(Math.Abs).OrderBy(x => x).ToArray();
}
