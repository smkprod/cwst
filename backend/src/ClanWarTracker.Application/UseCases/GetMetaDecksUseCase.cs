using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Лучшие колоды топа за неделю: игры, победы, процент, цикл и чего колода боится.
///
/// Считается по нашим суточным снимкам боёв, а не по профилям: профиль говорит,
/// чем игрок играет сейчас, а бои - чем выигрывают.
/// </summary>
public class GetMetaDecksUseCase(IClashRoyaleApi crApi, IMetaRepository meta)
{
    /// <summary>Окно в днях, включая последний.</summary>
    private const int WindowDays = 7;

    /// <summary>
    /// Меньше игр за неделю - процент побед ни о чём не говорит. Уилсон и так
    /// штрафует малые выборки, но совсем редкие колоды только засоряют список.
    /// </summary>
    private const int MinGames = 15;

    private const int Take = 30;

    /// <summary>В каждом бою 8 карт против 8: одна сторона даёт 64 пары.</summary>
    private const int PairsPerSide = 64;

    public async Task<MetaDecksDto?> ExecuteAsync(CancellationToken ct = default)
    {
        var latest = await meta.LatestDayAsync(ct);
        if (latest is null) return null;

        var from = DateOnly.Parse(latest).AddDays(-(WindowDays - 1)).ToString("yyyy-MM-dd");
        var deckRows = await meta.GetDecksSinceAsync(from, ct);
        var pairRows = await meta.GetMatchupsSinceAsync(from, ct);

        var pairs = MetaCounters.Sum(pairRows.Select(p => (p.CardKey, p.OppCardKey, p.Games, p.Wins)));

        // Колоды, сыгранные за день по разу, не хранятся, поэтому общее число
        // сыгранных колод берём из пар: каждая сторона каждого боя дала ровно 64.
        var sides = pairs.Values.Sum(v => (long)v.Games) / PairsPerSide;
        var battles = (int)(sides / 2);

        var catalog = await SafeCatalogAsync(ct);

        var decks = deckRows
            .GroupBy(d => d.DeckKey)
            .Select(g => (Key: g.Key, Games: g.Sum(x => x.Games), Wins: g.Sum(x => x.Wins), Draws: g.Sum(x => x.Draws)))
            .Where(d => d.Games >= MinGames)
            .OrderByDescending(d => MetaAggregator.WilsonLower(d.Wins, d.Games))
            .Take(Take)
            .Select(d =>
            {
                var keys = MetaCard.ParseDeckKey(d.Key);
                var cards = keys.Select(k => Card(k, catalog)).ToList();
                var counters = MetaCounters.For(keys, pairs)
                    .Select(c => new MetaCounterDto(
                        Card(c.OppKey, catalog),
                        Math.Round(c.WinRate * 100, 1),
                        Math.Round(c.Delta * 100, 1),
                        c.Games))
                    .ToList();

                return new MetaDeckRowDto(
                    Cards: cards,
                    Games: d.Games,
                    Wins: d.Wins,
                    Draws: d.Draws,
                    Losses: d.Games - d.Wins - d.Draws,
                    WinPercent: Math.Round(100.0 * d.Wins / d.Games, 1),
                    UsagePercent: sides == 0 ? 0 : Math.Round(100.0 * d.Games / sides, 2),
                    AvgElixir: cards.Count == 0 ? 0 : Math.Round(cards.Average(c => c.Elixir), 1),
                    CycleElixir: cards.Select(c => c.Elixir).OrderBy(e => e).Take(4).Sum(),
                    Counters: counters,
                    CopyLink: CopyLink(keys));
            })
            .ToList();

        return new MetaDecksDto(from, latest, battles, decks);
    }

    /// <summary>Карта по ключу меты; минус - эволюция.</summary>
    internal static MetaCardDto Card(int key, Dictionary<int, CrCatalogCard> catalog)
    {
        var id = Math.Abs(key);
        var evo = key < 0;
        var c = catalog.GetValueOrDefault(id);
        return new MetaCardDto(
            CardId: id,
            Name: c?.Name ?? $"#{id}",
            IconUrl: (evo ? c?.EvoIconUrl : null) ?? c?.IconUrl ?? "",
            Evo: evo,
            Elixir: c?.ElixirCost ?? 0);
    }

    private static string? CopyLink(int[] keys) =>
        keys.Length == 0
            ? null
            : $"https://link.clashroyale.com/deck/en?deck={string.Join(';', keys.Select(k => Math.Abs(k)))}";

    /// <summary>Справочник только для имён, иконок и эликсира; без него - номера вместо имён.</summary>
    private async Task<Dictionary<int, CrCatalogCard>> SafeCatalogAsync(CancellationToken ct)
    {
        try
        {
            var catalog = await crApi.GetAllCardsAsync(ct);
            return catalog.Values.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        }
        catch
        {
            return [];
        }
    }
}
