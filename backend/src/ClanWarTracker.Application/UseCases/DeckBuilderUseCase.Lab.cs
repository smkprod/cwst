using System.Text.Json;
using ClanWarTracker.Application.Meta;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Конструктор колоды: игрок собирает до восьми карт, а мы показываем, что получилось -
/// шкалы атаки, защиты, синергии и баланса, роли карт, связи между ними по реальным
/// колодам топа, чего не хватает и какими картами это закрыть, и похожие колоды из меты.
///
/// Шкалы - простые правила по ролям (есть ли победная карта, сколько ПВО, есть ли малое
/// и большое заклинание, нормальный ли эликсир), а синергия и подсказки - статистика:
/// как часто карты стоят вместе в колодах топа. Нейросеть здесь только подписывает связи
/// и размечает роли новых карт (CardKnowledgeUseCase).
/// </summary>
public partial class DeckBuilderUseCase
{
    public const string LearnedRolesKey = "cards.learned";
    public const string LinkNotesKey = "cards.links";

    public record CheckDto(string Key, bool Ok);
    public record RoleGroupDto(string Role, List<int> Cards);
    public record LinkDto(int A, int B, double CoPlayPercent, double WinPercent, int Games, string? Note);
    public record SuggestDto(CardDto Card, double Synergy, bool Weak);
    public record SuggestGroupDto(string Reason, List<SuggestDto> Cards);

    /// <param name="Attack">Шкалы 0..100.</param>
    public record LabResult(bool Linked, List<CardDto> Cards, double AvgElixir, int CycleElixir,
        int Attack, int Defense, int Synergy, int Balance, List<CheckDto> Checks, List<RoleGroupDto> Roles,
        List<LinkDto> Links, List<SuggestGroupDto> Suggestions, List<DeckDto> Similar, string? CopyLink);

    public record LinkNote(int A, int B, Dictionary<string, string> Note);

    /// <summary>Сколько карт показываем в каждой группе подсказок.</summary>
    private const int SuggestTake = 8;

    /// <summary>Ниже этой доли совместных игр карта с колодой почти не встречается - подсказка «слабая».</summary>
    private const double WeakSynergy = 0.05;

    public async Task<LabResult?> AnalyzeAsync(long tg, IReadOnlyList<int> cardIds, string? lang, CancellationToken ct = default)
    {
        var ctx = await ContextAsync(tg, lang, ct);
        if (ctx is null) return null;
        var syn = MetaSynergy.For(ctx.Window);
        var learned = await LearnedRolesAsync(ct);
        var notes = await LinkNotesAsync(lang, ct);

        var ids = cardIds.Where(id => ctx.Catalog.ContainsKey(id)).Distinct().Take(8).ToList();
        var cards = ids.Select(id => ToCard(id, false, ctx.Catalog, ctx.Mine)).ToList();
        string[] RolesOf(int id) => ctx.Catalog.TryGetValue(id, out var c)
            ? CardRoles.For(id, c.Name, c.ElixirCost, learned)
            : [];
        var roles = ids.ToDictionary(id => id, RolesOf);
        int Count(string role) => roles.Values.Count(r => r.Contains(role));

        var avgElixir = cards.Count == 0 ? 0 : Math.Round(cards.Average(c => c.Elixir), 1);
        var win = Count(CardRoles.Win);
        var antiAir = Count(CardRoles.AntiAir);
        var splash = Count(CardRoles.Splash);
        var small = Count(CardRoles.SmallSpell);
        var big = Count(CardRoles.BigSpell);

        // Шкалы. Простые и объяснимые правила: игрок видит, что именно двигает полоску.
        var support = ids.Count(id => !roles[id].Contains(CardRoles.Win)
                                      && (roles[id].Contains(CardRoles.Dps) || roles[id].Contains(CardRoles.Splash)));
        var attack = Math.Min(100, (win switch { 0 => 10, 1 => 55, 2 => 75, _ => 70 }) + (big > 0 ? 10 : 0) + Math.Min(2, support) * 8);
        var defense = Math.Min(100, new[] { 0, 30, 55, 70, 80 }[Math.Min(antiAir, 4)] + (splash > 0 ? 12 : 0)
            + (Count(CardRoles.Building) + Count(CardRoles.Tank) + Count(CardRoles.MiniTank) > 0 ? 8 : 0));

        var pairs = new List<(int A, int B, double Co)>();
        for (var i = 0; i < ids.Count; i++)
            for (var j = i + 1; j < ids.Count; j++)
                pairs.Add((ids[i], ids[j], Math.Max(syn.CoPlay(ids[i], ids[j]), syn.CoPlay(ids[j], ids[i]))));
        var synergy = pairs.Count == 0 ? 0 : (int)Math.Min(100, Math.Round(pairs.Average(p => p.Co) * 160));

        var checks = new List<CheckDto>
        {
            new("win", win >= 1),
            new("antiair", antiAir >= 2),
            new("splash", splash >= 1),
            new("smallspell", small >= 1),
            new("bigspell", big >= 1 || win >= 2),
            new("elixir", cards.Count > 0 && avgElixir is >= 2.6 and <= 4.3),
        };
        var balance = cards.Count == 0 ? 0 : (int)Math.Round(100.0 * checks.Count(c => c.Ok) / checks.Count);

        var groups = CardRoles.All
            .Select(r => new RoleGroupDto(r, ids.Where(id => roles[id].Contains(r)).ToList()))
            .Where(g => g.Cards.Count > 0)
            .ToList();

        var links = pairs
            .Select(p => (p.A, p.B, p.Co, Stat: syn.Pair(p.A, p.B)))
            .Where(p => p.Stat.Games >= 20 && p.Co >= 0.05)
            .OrderByDescending(p => p.Co)
            .Take(5)
            .Select(p => new LinkDto(p.A, p.B, Math.Round(p.Co * 100, 1),
                Math.Round(100.0 * p.Stat.Wins / p.Stat.Games, 1), p.Stat.Games,
                notes.GetValueOrDefault(MetaSynergy.Key(p.A, p.B))))
            .ToList();

        // Подсказки: каких ролей не хватает и какие карты их закрывают лучше всего вместе
        // с уже выбранными. Колода полная - подсказывать нечего, только связи и похожие.
        var suggestions = new List<SuggestGroupDto>();
        if (ids.Count < 8)
        {
            var maxGames = syn.Cards.Count == 0 ? 1 : syn.Cards.Values.Max(c => c.Games);
            double Fit(int c) => ids.Count == 0
                ? (double)syn.Cards.GetValueOrDefault(c).Games / Math.Max(1, maxGames)
                : ids.Average(s => syn.CoPlay(s, c));
            var pool = ctx.Catalog.Keys.Where(id => id < 159000000 && !ids.Contains(id)).ToList();

            List<SuggestDto> Pick(Func<int, bool> filter) => pool
                .Where(filter)
                .Select(id => (id, fit: Fit(id)))
                .OrderByDescending(x => x.fit)
                .Take(SuggestTake)
                .Select(x => new SuggestDto(ToCard(x.id, false, ctx.Catalog, ctx.Mine), Math.Round(x.fit * 100, 1),
                    ids.Count > 0 && x.fit < WeakSynergy))
                .ToList();

            if (ids.Count > 0) suggestions.Add(new SuggestGroupDto("best", Pick(_ => true)));
            foreach (var (key, role) in new[] { ("win", CardRoles.Win), ("antiair", CardRoles.AntiAir),
                         ("splash", CardRoles.Splash), ("smallspell", CardRoles.SmallSpell), ("bigspell", CardRoles.BigSpell) })
            {
                if (checks.First(c => c.Key == key).Ok) continue;
                var list = Pick(id => RolesOf(id).Contains(role));
                if (list.Count > 0) suggestions.Add(new SuggestGroupDto(key, list));
            }
        }

        // Похожие колоды из меты: где совпадает больше всего карт
        var similar = new List<DeckDto>();
        if (ids.Count >= 3)
        {
            var need = Math.Max(2, ids.Count - 2);
            similar = Candidates(ctx, new Filter([], [], null, false, "fit"))
                .Select(d => (d, overlap: d.Keys.Select(Math.Abs).Distinct().Count(ids.Contains)))
                .Where(x => x.overlap >= need && x.overlap < 8 || (x.overlap == 8 && ids.Count == 8))
                .OrderByDescending(x => x.overlap).ThenByDescending(x => x.d.Wilson)
                .Take(5)
                .Select(x => x.d.Dto)
                .ToList();
        }

        return new LabResult(ctx.Linked, cards, avgElixir, cards.Select(c => c.Elixir).OrderBy(e => e).Take(4).Sum(),
            attack, defense, synergy, balance, checks, groups, links, suggestions, similar,
            ids.Count == 8 ? GetMetaDecksUseCase.CopyLink(ids.ToArray()) : null);
    }

    /// <summary>Роли новых карт, которые разметила нейросеть: имя карты → роли.</summary>
    private async Task<Dictionary<string, string[]>> LearnedRolesAsync(CancellationToken ct)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                       await Settings.GetAsync(LearnedRolesKey, ct) ?? "{}") ?? [];
        }
        catch (JsonException) { return []; }
    }

    /// <summary>Подписи к связям карт на языке игрока: пара id (по возрастанию) → фраза.</summary>
    private async Task<Dictionary<(int, int), string>> LinkNotesAsync(string? lang, CancellationToken ct)
    {
        List<LinkNote>? list;
        try { list = JsonSerializer.Deserialize<List<LinkNote>>(await Settings.GetAsync(LinkNotesKey, ct) ?? "[]"); }
        catch (JsonException) { return []; }
        var l = lang is "uk" or "en" ? lang : "ru";
        var result = new Dictionary<(int, int), string>();
        foreach (var n in list ?? [])
        {
            var text = n.Note.GetValueOrDefault(l) ?? n.Note.GetValueOrDefault("ru");
            if (!string.IsNullOrWhiteSpace(text)) result[MetaSynergy.Key(n.A, n.B)] = text;
        }
        return result;
    }
}
