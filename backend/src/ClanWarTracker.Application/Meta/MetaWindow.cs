using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Мета за неделю, сложенная по дням: колоды и пары «карта против карты».
///
/// Пар за неделю - сотни тысяч строк, а меняются они раз в сутки, со снимком.
/// Читать их из базы на каждое открытие вкладки или разбора - расточительство,
/// поэтому держим сложенную неделю в памяти процесса и перечитываем, когда
/// появился новый день или прошло четверть часа (снимок могли пересобрать).
/// </summary>
public static class MetaWindow
{
    /// <summary>Окно в днях, включая последний.</summary>
    public const int Days = 7;

    /// <summary>В каждом бою 8 карт против 8: одна сторона даёт 64 пары.</summary>
    private const int PairsPerSide = 64;

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Data? _cached;
    private static DateTime _loadedAtUtc;

    /// <param name="Sides">
    /// Сколько колод сыграно за окно всего. Колоды, сыгранные за день по разу, не
    /// хранятся, поэтому считаем из пар: каждая сторона каждого боя дала ровно 64.
    /// </param>
    public sealed record Data(
        string FromDayUtc,
        string ToDayUtc,
        IReadOnlyList<(string Key, int Games, int Wins, int Draws)> Decks,
        Dictionary<(int Card, int Opp), (int Games, int Wins)> Pairs,
        long Sides)
    {
        public int Battles => (int)(Sides / 2);
    }

    /// <returns>null - меты ещё нет.</returns>
    public static async Task<Data?> LoadAsync(IMetaRepository meta, CancellationToken ct)
    {
        var latest = await meta.LatestDayAsync(ct);
        if (latest is null) return null;

        var cached = _cached;
        if (cached is not null && cached.ToDayUtc == latest && DateTime.UtcNow - _loadedAtUtc < Ttl)
            return cached;

        await Gate.WaitAsync(ct);
        try
        {
            cached = _cached;
            if (cached is not null && cached.ToDayUtc == latest && DateTime.UtcNow - _loadedAtUtc < Ttl)
                return cached;

            var from = DateOnly.Parse(latest).AddDays(-(Days - 1)).ToString("yyyy-MM-dd");
            var decks = await meta.GetDeckTotalsSinceAsync(from, ct);
            var pairRows = await meta.GetMatchupTotalsSinceAsync(from, ct);

            var pairs = new Dictionary<(int, int), (int Games, int Wins)>(pairRows.Count);
            long pairGames = 0;
            foreach (var p in pairRows)
            {
                pairs[(p.CardKey, p.OppCardKey)] = (p.Games, p.Wins);
                pairGames += p.Games;
            }

            var data = new Data(
                from,
                latest,
                decks.Select(d => (d.DeckKey, d.Games, d.Wins, d.Draws)).ToList(),
                pairs,
                pairGames / PairsPerSide);

            _cached = data;
            _loadedAtUtc = DateTime.UtcNow;
            return data;
        }
        finally
        {
            Gate.Release();
        }
    }
}
