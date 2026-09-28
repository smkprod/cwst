using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Архетип колоды по её картам: «Golem», «Hog Cycle», «Log Bait».
///
/// Игрок думает архетипами, а не колодами: «против Голема я сливаю», а не «против
/// колоды 26000009,26000015,...». Точная колода соперника почти не повторяется, а
/// архетип повторяется каждый вечер - на нём и копится «счёт против».
///
/// Правила - упорядоченный список, первое совпадение выигрывает: Lava + Balloon
/// раньше просто Lava, Hog + EQ раньше просто Hog. Названия латиницей и одни на
/// все языки - так их называют сами игроки.
/// </summary>
public static class Archetypes
{
    private record Rule(string Key, string Label, string KeyCard, string[] All, string[]? AnyOf = null, double? MaxAvg = null);

    private static readonly Rule[] Rules =
    [
        new("xbow", "X-Bow", "X-Bow", ["X-Bow"]),
        new("mortar", "Mortar", "Mortar", ["Mortar"]),
        new("lavaloon", "LavaLoon", "Lava Hound", ["Lava Hound", "Balloon"]),
        new("lava", "Lava", "Lava Hound", ["Lava Hound"]),
        new("golem", "Golem", "Golem", ["Golem"]),
        new("egolem", "E-Golem", "Elixir Golem", ["Elixir Golem"]),
        new("egiant", "E-Giant", "Electro Giant", ["Electro Giant"]),
        new("ggiant", "Goblin Giant", "Goblin Giant", ["Goblin Giant"]),
        new("giantgy", "Giant GY", "Graveyard", ["Giant", "Graveyard"]),
        new("graveyard", "Graveyard", "Graveyard", ["Graveyard"]),
        new("rg", "Royal Giant", "Royal Giant", ["Royal Giant"]),
        new("hogeq", "Hog EQ", "Hog Rider", ["Hog Rider", "Earthquake"]),
        new("hogcycle", "Hog Cycle", "Hog Rider", ["Hog Rider"], MaxAvg: 3.1),
        new("hog", "Hog", "Hog Rider", ["Hog Rider"]),
        new("royalhogs", "Royal Hogs", "Royal Hogs", ["Royal Hogs"]),
        new("pekkabs", "PEKKA BS", "P.E.K.K.A", ["P.E.K.K.A"], AnyOf: ["Battle Ram", "Ram Rider", "Bandit"]),
        new("ramrider", "Ram Rider", "Ram Rider", ["Ram Rider"]),
        new("balloon", "Balloon", "Balloon", ["Balloon"]),
        new("drill", "Drill", "Goblin Drill", ["Goblin Drill"]),
        new("minerpoison", "Miner Poison", "Miner", ["Miner", "Poison"]),
        new("miner", "Miner", "Miner", ["Miner"]),
        new("logbait", "Log Bait", "Goblin Barrel", ["Goblin Barrel"]),
        new("3m", "3M", "Three Musketeers", ["Three Musketeers"]),
        new("pekka", "PEKKA", "P.E.K.K.A", ["P.E.K.K.A"]),
        new("mk", "Mega Knight", "Mega Knight", ["Mega Knight"]),
        new("sparky", "Sparky", "Sparky", ["Sparky"]),
        new("wb", "Wall Breakers", "Wall Breakers", ["Wall Breakers"]),
        new("giant", "Giant", "Giant", ["Giant"]),
        new("ram", "Battle Ram", "Battle Ram", ["Battle Ram"]),
    ];

    private static readonly Dictionary<string, Rule> ByKey = Rules.ToDictionary(r => r.Key);

    public const string OtherPrefix = "other:";

    /// <param name="cards">Карты колоды: английское имя, id и стоимость (null - неизвестна).</param>
    /// <returns>Ключ архетипа или other:&lt;id самой дорогой карты&gt;. null - колоды нет.</returns>
    public static string? Classify(IReadOnlyList<(string Name, int Id, int? Elixir)> cards)
    {
        if (cards.Count == 0) return null;
        var names = cards.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var known = cards.Where(c => c.Elixir is > 0).Select(c => c.Elixir!.Value).ToList();
        // Средняя стоимость нужна одному правилу (Hog Cycle); неизвестна - правило не срабатывает.
        double? avg = known.Count == cards.Count ? known.Average() : null;

        foreach (var r in Rules)
        {
            if (!r.All.All(names.Contains)) continue;
            if (r.AnyOf is not null && !r.AnyOf.Any(names.Contains)) continue;
            if (r.MaxAvg is double max && !(avg <= max)) continue;
            return r.Key;
        }

        var top = cards.OrderByDescending(c => c.Elixir ?? 0).ThenBy(c => c.Id).First();
        return OtherPrefix + top.Id;
    }

    /// <summary>Архетип колоды из ключа колоды (для боёв, записанных до трекера) - через справочник карт.</summary>
    public static string? ClassifyKey(string deckKey, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var cards = new List<(string, int, int?)>();
        foreach (var k in MetaCard.ParseDeckKey(deckKey))
        {
            var c = catalog.GetValueOrDefault(Math.Abs(k));
            if (c is null) return null;   // справочник неполон - лучше без архетипа, чем с неверным
            cards.Add((c.Name, c.Id, c.ElixirCost > 0 ? c.ElixirCost : null));
        }
        return Classify(cards);
    }

    public static bool IsOther(string key) => key.StartsWith(OtherPrefix, StringComparison.Ordinal);

    /// <summary>Название архетипа. У «прочих» - имя их самой дорогой карты (в текстах оборачивается в TrkOther).</summary>
    public static string Label(string key, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        if (ByKey.TryGetValue(key, out var r)) return r.Label;
        if (IsOther(key) && int.TryParse(key[OtherPrefix.Length..], out var id))
            return catalog.GetValueOrDefault(id)?.Name ?? "?";
        return key;
    }

    /// <summary>Главная карта архетипа - её иконку показываем рядом с названием.</summary>
    public static int? KeyCard(string key, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        if (ByKey.TryGetValue(key, out var r))
            return catalog.Values.FirstOrDefault(c => string.Equals(c.Name, r.KeyCard, StringComparison.OrdinalIgnoreCase))?.Id;
        if (IsOther(key) && int.TryParse(key[OtherPrefix.Length..], out var id)) return id;
        return null;
    }
}
