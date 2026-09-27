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
public class GetMetaDecksUseCase(IClashRoyaleApi crApi, IMetaRepository meta, ITopPlayerRepository top)
{

    /// <summary>
    /// Меньше игр за неделю - процент побед ни о чём не говорит. Уилсон и так
    /// штрафует малые выборки, но совсем редкие колоды только засоряют список.
    /// </summary>
    private const int MinGames = 15;

    private const int Take = 30;


    /// <summary>Сколько колод показываем в выборке по одной карте.</summary>
    private const int TakeForCard = 10;

    /// <summary>
    /// Колоды с одной картой: из боёв (если мета есть) и из профилей топа.
    /// Карта ищется и обычной, и эволюцией: игрок, тапнувший «Рыцаря», хочет
    /// видеть колоды с Рыцарем, а какая он там версии - уже видно на иконке.
    /// </summary>
    public async Task<MetaDecksDto?> ForCardAsync(int cardId, CancellationToken ct = default)
    {
        var battle = await QueryAsync(ct, keys => keys.Any(k => Math.Abs(k) == cardId), TakeForCard);
        var catalog = await SafeCatalogAsync(crApi, ct);

        var profile = new List<TopProfileDeckDto>();
        var latestTop = await top.LatestDayAsync(ct);
        if (latestTop is not null)
        {
            var rows = await top.GetDayAsync(latestTop, ct);
            profile = rows
                .Where(r => r.DeckCardIds is not null)
                .Select(r => (r.Rank, Ids: r.DeckCardIds!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).OrderBy(id => id).ToArray()))
                .Where(r => r.Ids.Contains(cardId))
                .GroupBy(r => string.Join(',', r.Ids))
                .Select(g => (Ids: g.First().Ids, Players: g.Count(), BestRank: g.Min(x => x.Rank)))
                .OrderByDescending(g => g.Players)
                .ThenBy(g => g.BestRank)
                .Take(TakeForCard)
                .Select(g => new TopProfileDeckDto(
                    g.Ids.Select(id => Card(id, catalog)).ToList(),
                    g.Players,
                    g.BestRank,
                    CopyLink(g.Ids)))
                .ToList();
        }

        if (battle is null && profile.Count == 0) return null;
        return battle is null
            ? new MetaDecksDto("", latestTop ?? "", 0, [], profile)
            : battle with { ProfileDecks = profile };
    }

    public Task<MetaDecksDto?> ExecuteAsync(CancellationToken ct = default) => QueryAsync(ct, null, Take);

    private async Task<MetaDecksDto?> QueryAsync(CancellationToken ct, Func<int[], bool>? filter, int take)
    {
        var window = await MetaWindow.LoadAsync(meta, ct);
        if (window is null) return null;

        var catalog = await SafeCatalogAsync(crApi, ct);

        var decks = window.Decks
            .Where(d => d.Games >= MinGames)
            .Where(d => filter is null || filter(MetaCard.ParseDeckKey(d.Key)))
            .OrderByDescending(d => MetaAggregator.WilsonLower(d.Wins, d.Games))
            .Take(take)
            .Select(d =>
            {
                var keys = MetaCard.ParseDeckKey(d.Key);
                var cards = keys.Select(k => Card(k, catalog)).ToList();
                var counters = MetaCounters.For(keys, window.Pairs)
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
                    UsagePercent: window.Sides == 0 ? 0 : Math.Round(100.0 * d.Games / window.Sides, 2),
                    AvgElixir: cards.Count == 0 ? 0 : Math.Round(cards.Average(c => c.Elixir), 1),
                    CycleElixir: cards.Select(c => c.Elixir).OrderBy(e => e).Take(4).Sum(),
                    Counters: counters,
                    CopyLink: CopyLink(keys));
            })
            .ToList();

        return new MetaDecksDto(window.FromDayUtc, window.ToDayUtc, window.Battles, decks);
    }

    /// <summary>Карта по ключу меты; минус - эволюция.</summary>
    public static MetaCardDto Card(int key, Dictionary<int, CrCatalogCard> catalog)
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

    public static string? CopyLink(int[] keys) =>
        keys.Length == 0
            ? null
            : $"https://link.clashroyale.com/deck/en?deck={string.Join(';', keys.Select(k => Math.Abs(k)))}";

    /// <summary>Справочник только для имён, иконок и эликсира; без него - номера вместо имён.</summary>
    public static async Task<Dictionary<int, CrCatalogCard>> SafeCatalogAsync(IClashRoyaleApi crApi, CancellationToken ct)
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
