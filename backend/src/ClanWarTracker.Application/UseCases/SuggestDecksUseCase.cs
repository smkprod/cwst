using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Примеряет базу мета-колод на коллекцию игрока и отвечает на вопрос «во что мне играть
/// прямо сейчас». Считаем честно: колода «готова», только если открыты все 8 карт —
/// колода без одной карты в игре просто не собирается, и обещать обратное нельзя.
///
/// Иконки и стоимость эликсира берём из справочника /cards, а не из профиля: в профиле
/// есть только открытые карты, а показать надо и те, которых не хватает.
/// </summary>
public class SuggestDecksUseCase(IClashRoyaleApi crApi)
{
    private const int DeckSize = 8;

    // Подбор теперь живёт в отдельной шторке, а не в куске вкладки, — места хватает.
    // Верхнюю границу всё же держим: список из 90 колод никто не дочитает.
    private const int MaxReady = 20;
    private const int MaxAlmost = 12;

    /// <summary>Сколько карт может не хватать, чтобы колода попала в «почти собрана».</summary>
    private const int AlmostGap = 2;

    public async Task<DeckSuggestionsDto?> ExecuteAsync(string playerTag, CancellationToken ct = default)
    {
        var info = await crApi.GetPlayerInfoAsync(playerTag, ct);
        if (info is null) return null;

        var catalog = await crApi.GetAllCardsAsync(ct);
        // Без справочника нечем показывать недостающие карты — отдаём пусто, а не полуправду
        if (catalog.Count == 0) return Empty(playerTag, info.MaxCardLevel);

        var owned = info.Cards.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var maxLevel = info.MaxCardLevel > 0 ? info.MaxCardLevel : 15;

        var scored = new List<DeckSuggestionDto>();
        var skipped = 0;
        foreach (var deck in MetaDecks.All)
        {
            // Колода с картой, которой нет в справочнике, — опечатка в нашей базе или
            // сезонное переименование карты. Показывать её нельзя: игрок увидит колоду,
            // которую не сможет собрать никогда. Счётчик отброшенных отдаём наружу,
            // иначе база молча усохнет и никто этого не заметит.
            if (deck.Cards.Any(name => !catalog.ContainsKey(name))) { skipped++; continue; }

            scored.Add(Score(deck, catalog, owned, maxLevel));
        }

        var ready = scored
            .Where(d => d.OwnedCount == DeckSize)
            .OrderByDescending(d => d.Readiness)
            .ThenByDescending(d => d.AvgLevel)
            .Take(MaxReady)
            .ToList();

        var almost = scored
            .Where(d => d.OwnedCount < DeckSize && DeckSize - d.OwnedCount <= AlmostGap)
            .OrderBy(d => d.Missing.Count)
            .ThenByDescending(d => d.AvgLevel)
            .Take(MaxAlmost)
            .ToList();

        return new DeckSuggestionsDto(
            PlayerTag: info.Tag,
            MaxCardLevel: maxLevel,
            BaseUpdated: MetaDecks.UpdatedLabel,
            BaseSource: MetaDecks.SourceNote,
            BaseSize: scored.Count,
            BaseSkipped: skipped,
            Ready: ready,
            Almost: almost);
    }

    private static DeckSuggestionsDto Empty(string playerTag, int maxLevel) => new(
        PlayerTag: playerTag,
        MaxCardLevel: maxLevel,
        BaseUpdated: MetaDecks.UpdatedLabel,
        BaseSource: MetaDecks.SourceNote,
        BaseSize: MetaDecks.All.Count,
        BaseSkipped: 0,
        Ready: [],
        Almost: []);

    private static DeckSuggestionDto Score(
        MetaDecks.MetaDeck deck,
        IReadOnlyDictionary<string, CrCatalogCard> catalog,
        IReadOnlyDictionary<string, CrCard> owned,
        int maxLevel)
    {
        var cards = new List<DeckCardDto>(DeckSize);
        var missing = new List<string>();

        foreach (var name in deck.Cards)
        {
            var meta = catalog[name];
            owned.TryGetValue(name, out var mine);
            if (mine is null) missing.Add(name);

            var evoUnlockedHere = mine is not null && mine.EvolutionLevel > 0;
            // Открыл эволюцию — показываем её арт, как в игре
            var icon = evoUnlockedHere && mine!.EvoIconUrl is not null ? mine.EvoIconUrl : meta.IconUrl;

            cards.Add(new DeckCardDto(
                Name: meta.Name,
                Level: mine?.Level ?? 0,
                MaxLevel: maxLevel,
                ElixirCost: meta.ElixirCost,
                IconUrl: icon,
                Owned: mine is not null,
                EvoUnlocked: evoUnlockedHere,
                HasEvo: meta.MaxEvolutionLevel > 0,
                EvoIconUrl: meta.EvoIconUrl));
        }

        var ownedCards = cards.Where(c => c.Owned).ToList();
        var ownedCount = ownedCards.Count;
        var avgLevel = ownedCount > 0 ? Math.Round(ownedCards.Average(c => c.Level), 1) : 0;
        var maxed = ownedCards.Count(c => c.Level >= maxLevel);
        var evoAvailable = cards.Count(c => c.HasEvo);
        var evoUnlocked = cards.Count(c => c.EvoUnlocked);

        // Средний эликсир считаем по всей колоде — это свойство самой колоды,
        // а не коллекции игрока, поэтому недостающие карты тоже учитываются.
        var avgElixir = Math.Round(cards.Average(c => c.ElixirCost), 1);

        var readiness = Readiness(ownedCount, avgLevel, maxLevel);

        return new DeckSuggestionDto(
            Id: deck.Id,
            Name: deck.Name,
            Archetype: deck.Archetype,
            Difficulty: deck.Difficulty,
            Note: deck.Note,
            Cards: cards,
            OwnedCount: ownedCount,
            Missing: missing,
            AvgLevel: avgLevel,
            AvgElixir: avgElixir,
            MaxedCount: maxed,
            EvoUnlocked: evoUnlocked,
            EvoAvailable: evoAvailable,
            Readiness: readiness,
            Verdict: Verdict(ownedCount, missing, avgLevel, maxLevel));
    }

    /// <summary>
    /// 0..100. Половина веса — открыты ли все карты (без этого колода не существует),
    /// половина — насколько они прокачаны относительно потолка.
    /// </summary>
    private static int Readiness(int ownedCount, double avgLevel, int maxLevel)
    {
        var completeness = ownedCount / (double)DeckSize;
        var levelShare = maxLevel > 0 ? Math.Clamp(avgLevel / maxLevel, 0, 1) : 0;
        return (int)Math.Round((completeness * 0.5 + levelShare * 0.5) * 100);
    }

    private static string Verdict(int ownedCount, List<string> missing, double avgLevel, int maxLevel)
    {
        if (ownedCount < DeckSize)
        {
            var word = missing.Count == 1 ? "карты" : "карт";
            return $"Не хватает {missing.Count} {word}: {string.Join(", ", missing)}";
        }

        var gap = maxLevel - avgLevel;
        return gap switch
        {
            <= 0.5 => $"Колода собрана на максимуме ({avgLevel} из {maxLevel}) — можно играть в войне",
            <= 2 => $"Колода готова, средний уровень {avgLevel} из {maxLevel}",
            <= 4 => $"Все карты есть, но качать ещё есть что: {avgLevel} из {maxLevel}",
            _ => $"Карты собраны, уровни слабые: {avgLevel} из {maxLevel}",
        };
    }
}
