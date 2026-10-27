using System.Text.Json;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Билдер колод: мета-колоды из боёв топа за неделю, которые игрок может собрать из
/// своих карт, и четыре колоды для клановой войны без общих карт.
///
/// Колоды не придумываются, а берутся только из реально сыгранных в топе: у каждой есть
/// игры и процент побед. Под игрока они сортируются по «подходят мне» - процент побед
/// (нижняя граница Уилсона, чтобы колода с пятью играми не обгоняла колоду с пятьюстами)
/// минус штраф за недокачанные и отсутствующие карты.
/// </summary>
public partial class DeckBuilderUseCase(
    IClashRoyaleApi crApi,
    IMetaRepository meta,
    IPlayerRepository players,
    IServiceSettingRepository settings)
{
    /// <summary>Настройки - для второй половины класса (конструктор), куда параметры конструктора не видны.</summary>
    private IServiceSettingRepository Settings => settings;

    /// <summary>Меньше игр за неделю - процент побед ничего не значит.</summary>
    public const int MinGames = 10;

    private const int Take = 40;

    /// <summary>Из скольких лучших колод ищем четвёрку для КВ: больше - перебор дольше, а лучше не станет.</summary>
    private const int WarCandidates = 90;

    /// <summary>Штраф «подходит мне» за каждый недостающий до максимума уровень карты в среднем по колоде.</summary>
    private const double LevelPenalty = 0.02;

    /// <summary>Карты нет вовсе - считаем как пять уровней недокачки: колоду придётся собирать.</summary>
    private const int MissingCardLevels = 5;

    /// <param name="BaseIconUrl">Обычный арт карты - запасной, если арт эволюции не открылся.</param>
    /// <param name="HeroIconUrl">Арт Героя карты - для тренажёра; null - Героя у карты нет.</param>
    /// <param name="EvoIconUrl">Арт эволюции карты - для тренажёра; null - эволюции нет.</param>
    public record CardDto(int CardId, string Name, string IconUrl, string BaseIconUrl, bool Evo, int Elixir, string Rarity,
        int Level, int MaxLevel, bool Owned, bool EvoOwned, string? HeroIconUrl = null, string? EvoIconUrl = null);

    /// <param name="Key">Ключ колоды в мете - по нему же отмечен выбор аналитика.</param>
    /// <param name="Owned">Сколько из восьми карт у игрока есть.</param>
    /// <param name="AvgLevel">Средний уровень своих карт колоды (как в игре); 0 - игрок не привязан.</param>
    /// <param name="Score">«Подходит мне» в процентах - то, по чему сортируется список.</param>
    public record DeckDto(string Key, List<CardDto> Cards, int Games, double WinPercent, double UsagePercent,
        double AvgElixir, int CycleElixir, int Owned, double AvgLevel, double Score, string? CopyLink, PickDto? Pick);

    public record PickDto(string Tag, string Note);

    public record AnalystDto(string Day, string Summary, int Picks);

    /// <param name="Linked">Тег привязан - уровни и «только мои карты» работают.</param>
    public record DecksResult(string FromDayUtc, string ToDayUtc, int Battles, bool Linked, List<DeckDto> Decks,
        AnalystDto? Analyst);

    /// <param name="Complete">Нашлись все четыре колоды без общих карт.</param>
    public record WarResult(bool Linked, bool Complete, List<DeckDto> Decks, double AvgWinPercent, int UniqueCards);

    public record Filter(IReadOnlyCollection<int> Include, IReadOnlyCollection<int> Exclude, double? MaxElixir,
        bool OnlyOwned, string Sort);

    /* ---------------- Мета-колоды ---------------- */

    public async Task<DecksResult?> DecksAsync(long tg, Filter f, string? lang, CancellationToken ct = default)
    {
        var ctx = await ContextAsync(tg, lang, ct);
        if (ctx is null) return null;
        var decks = Candidates(ctx, f);
        decks = f.Sort switch
        {
            "win" => decks.OrderByDescending(d => d.Wilson).ToList(),
            "popular" => decks.OrderByDescending(d => d.Dto.Games).ToList(),
            _ => decks.OrderByDescending(d => d.Fit).ToList(),
        };
        return new DecksResult(ctx.Window.FromDayUtc, ctx.Window.ToDayUtc, ctx.Window.Battles, ctx.Linked,
            decks.Take(Take).Select(d => d.Dto).ToList(), ctx.Analyst);
    }

    /* ---------------- Колоды для КВ ---------------- */

    /// <summary>
    /// Четыре колоды без общих карт с наибольшей суммой «подходит мне». Эволюция и обычная
    /// версия - одна и та же карта: в игре её не положить в две колоды сразу.
    /// </summary>
    public async Task<WarResult?> WarAsync(long tg, Filter f, string? lang, CancellationToken ct = default)
    {
        var ctx = await ContextAsync(tg, lang, ct);
        return ctx is null ? null : War(ctx, f);
    }

    /// <summary>Своя колода игрока из его боёв - тоже кандидат в четвёрку КВ.</summary>
    public record OwnDeck(string Key, int Games, int Wins);

    /// <summary>
    /// Подбор четырёх колод КВ для любой коллекции - для «Военного штаба», который смотрит
    /// весь клан разом: мету и справочник грузим один раз, а коллекции подставляем по
    /// одной. Кандидаты - колоды топа, которые игрок может собрать целиком, и его
    /// собственные колоды из боёв. null - меты ещё нет.
    /// </summary>
    public async Task<Func<Dictionary<string, CrCard>, IReadOnlyCollection<OwnDeck>, WarResult>?> WarPlannerAsync(CancellationToken ct = default)
    {
        var window = await MetaWindow.LoadAsync(meta, ct);
        if (window is null) return null;
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var all = new Filter([], [], null, OnlyOwned: true, "fit");
        return (mine, own) => War(new Ctx(window, catalog, mine, [], null), all, own);
    }

    /// <summary>Меньше игр в топе - колода редкая, её процент побед делают один-два игрока.</summary>
    public const int WarMinGames = 30;

    /// <summary>Сколько «нейтральных» игр (50%) подмешиваем к проценту побед - чтобы 90% на 40 играх не перевешивали.</summary>
    private const double WarPriorGames = 100;

    /// <summary>Сколько уровней стоит процент побед: 10 п.п. сверх 50% - полуровня, но не больше полуровня.</summary>
    private const double WarWinWeight = 5;
    private const double WarWinCap = 0.5;

    /// <summary>
    /// Оценка колоды для КВ: уровень прежде всего. На войне карты не выравниваются, и
    /// уровень разницы - это примерно +10% здоровья и урона в каждом бою. Процент побед в
    /// топе учитываем слабо и с поправкой на число игр: его делают топ-игроки, а не колода,
    /// и у редких колод он доходит до 85-90%. Так колода на два уровня ниже не обгонит
    /// свою, сколько бы она ни выигрывала в топе.
    /// </summary>
    public static double WarScore(double avgLevel, int wins, int games)
    {
        var shrunk = (wins + WarPriorGames / 2) / (games + WarPriorGames);
        return avgLevel + Math.Clamp((shrunk - 0.5) * WarWinWeight, -WarWinCap, WarWinCap);
    }

    /// <summary>Средний уровень восьми карт; карты, которой нет, - ноль: её в колоду не положить.</summary>
    private static double WarLevel(IEnumerable<CardDto> cards) => cards.Average(c => c.Owned ? c.Level : 0);

    /// <summary>Колода из журнала (ключ из id карт) с уровнями по коллекции - для «чем играл».</summary>
    public async Task<Func<string, Dictionary<string, CrCard>, DeckDto?>?> DeckReaderAsync(CancellationToken ct = default)
    {
        var window = await MetaWindow.LoadAsync(meta, ct);
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var games = window?.Decks.Where(d => d.Games >= MinGames).ToDictionary(d => d.Key, d => d) ?? [];
        return (key, mine) =>
        {
            var keys = MetaCard.ParseDeckKey(key);
            if (keys.Length != 8) return null;
            var cards = keys.Select(k => ToCard(Math.Abs(k), k < 0, catalog, mine)).ToList();
            var owned = cards.Where(c => c.Owned).ToList();
            var seen = games.TryGetValue(key, out var known);
            return new DeckDto(key, cards, seen ? known.Games : 0,
                seen ? Math.Round(100.0 * known.Wins / known.Games, 1) : 0, 0,
                Math.Round(cards.Average(c => c.Elixir), 1), cards.Select(c => c.Elixir).OrderBy(e => e).Take(4).Sum(),
                owned.Count, owned.Count == 0 ? 0 : Math.Round(owned.Average(c => c.Level), 1), 0,
                GetMetaDecksUseCase.CopyLink(keys), null);
        };
    }

    private WarResult War(Ctx ctx, Filter f, IReadOnlyCollection<OwnDeck>? own = null)
    {
        var meta = Candidates(ctx, f with { Include = [] })
            .Where(d => d.Dto.Games >= WarMinGames)
            .Select(d => (d.Dto, d.Keys,
                Score: WarScore(WarLevel(d.Dto.Cards), (int)Math.Round(d.Dto.WinPercent * d.Dto.Games / 100), d.Dto.Games)))
            .ToList();
        var metaKeys = meta.Select(m => m.Dto.Key).ToHashSet();

        // Свои колоды игрока - только те, что собираются из его карт целиком и которых нет в мете
        var mine = new List<(DeckDto Dto, int[] Keys, double Score)>();
        foreach (var o in own ?? [])
        {
            if (metaKeys.Contains(o.Key)) continue;
            var keys = MetaCard.ParseDeckKey(o.Key);
            if (keys.Length != 8) continue;
            var cards = keys.Select(k => ToCard(Math.Abs(k), k < 0, ctx.Catalog, ctx.Mine)).ToList();
            if (cards.Any(c => !c.Owned)) continue;
            var avgElixir = Math.Round(cards.Average(c => c.Elixir), 1);
            if (f.MaxElixir is double max && avgElixir > max + 0.001) continue;
            mine.Add((new DeckDto(o.Key, cards, 0, 0, 0, avgElixir, cards.Select(c => c.Elixir).OrderBy(e => e).Take(4).Sum(),
                8, Math.Round(cards.Average(c => c.Level), 1), 0, GetMetaDecksUseCase.CopyLink(keys), null),
                keys, WarScore(WarLevel(cards), o.Wins, o.Games)));
            metaKeys.Add(o.Key);
        }

        var cand = meta.Concat(mine)
            .OrderByDescending(d => d.Score)
            .Take(WarCandidates)
            .Select(d => (Deck: d, Cards: d.Keys.Select(Math.Abs).ToHashSet()))
            .ToList();

        // Перебор с отсечением: кандидаты отсортированы по убыванию, поэтому если даже
        // лучшие оставшиеся не дотягивают до рекорда - дальше смотреть незачем.
        var best = new List<int>();
        var bestScore = double.MinValue;
        var chosen = new List<int>();
        var used = new HashSet<int>();

        void Search(int start, double sum)
        {
            if (chosen.Count > best.Count || (chosen.Count == best.Count && sum > bestScore))
            {
                best = [.. chosen];
                bestScore = sum;
            }
            if (chosen.Count == 4) return;
            for (var i = start; i < cand.Count; i++)
            {
                var left = 4 - chosen.Count;
                if (best.Count == 4 && sum + cand[i].Deck.Score * left <= bestScore) break;
                if (cand[i].Cards.Overlaps(used)) continue;
                chosen.Add(i);
                used.UnionWith(cand[i].Cards);
                Search(i + 1, sum + cand[i].Deck.Score);
                used.ExceptWith(cand[i].Cards);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }
        Search(0, 0);

        var picked = best.Select(i => cand[i].Deck.Dto).ToList();
        return new WarResult(ctx.Linked, picked.Count == 4, picked,
            picked.Any(d => d.Games > 0) ? Math.Round(picked.Where(d => d.Games > 0).Average(d => d.WinPercent), 1) : 0,
            picked.SelectMany(d => d.Cards.Select(c => c.CardId)).Distinct().Count());
    }

    /* ---------------- Карты для фильтра ---------------- */

    /// <summary>Все карты игры с уровнями игрока - для выбора «с картой» / «без карты».</summary>
    public async Task<List<CardDto>> CardsAsync(long tg, CancellationToken ct = default)
    {
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var mine = await CollectionAsync(tg, ct);
        return catalog.Values
            .Where(c => c.Id < 159000000) // башенные войска в колоду не кладутся
            .OrderBy(c => c.ElixirCost).ThenBy(c => c.Name)
            .Select(c => ToCard(c.Id, false, catalog, mine))
            .ToList();
    }

    /* ---------------- Общее ---------------- */

    private sealed record Ctx(MetaWindow.Data Window, Dictionary<int, CrCatalogCard> Catalog,
        Dictionary<string, CrCard>? Mine, Dictionary<string, PickDto> Picks, AnalystDto? Analyst)
    {
        public bool Linked => Mine is not null;
    }

    private sealed record Scored(DeckDto Dto, int[] Keys, double Wilson, double Fit);

    private async Task<Ctx?> ContextAsync(long tg, string? lang, CancellationToken ct)
    {
        var window = await MetaWindow.LoadAsync(meta, ct);
        if (window is null) return null;
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var mine = await CollectionAsync(tg, ct);
        var (picks, analyst) = await AnalystAsync(lang, ct);
        return new Ctx(window, catalog, mine, picks, analyst);
    }

    /// <summary>Коллекция игрока по имени карты. null - тег не привязан или профиль не открылся.</summary>
    private async Task<Dictionary<string, CrCard>?> CollectionAsync(long tg, CancellationToken ct)
    {
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return null;
        try
        {
            var info = await crApi.GetPlayerInfoAsync(player.PlayerTag, ct);
            return info?.Cards
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
    }

    private List<Scored> Candidates(Ctx ctx, Filter f)
    {
        var include = f.Include.ToHashSet();
        var exclude = f.Exclude.ToHashSet();
        var list = new List<Scored>();
        foreach (var d in ctx.Window.Decks)
        {
            if (d.Games < MinGames) continue;
            var keys = MetaCard.ParseDeckKey(d.Key);
            if (keys.Length != 8) continue;
            var ids = keys.Select(Math.Abs).ToHashSet();
            if (include.Count > 0 && !include.All(ids.Contains)) continue;
            if (exclude.Count > 0 && exclude.Overlaps(ids)) continue;

            var cards = keys.Select(k => ToCard(Math.Abs(k), k < 0, ctx.Catalog, ctx.Mine)).ToList();
            var avgElixir = Math.Round(cards.Average(c => c.Elixir), 1);
            if (f.MaxElixir is double max && avgElixir > max + 0.001) continue;
            var owned = cards.Count(c => c.Owned);
            if (f.OnlyOwned && ctx.Linked && owned < 8) continue;

            var wilson = MetaAggregator.WilsonLower(d.Wins, d.Games);
            var deficit = ctx.Linked
                ? cards.Sum(c => c.Owned ? Math.Max(0, c.MaxLevel - c.Level) : MissingCardLevels) / 8.0
                : 0;
            var fit = wilson - LevelPenalty * deficit;
            var ownedCards = cards.Where(c => c.Owned).ToList();

            list.Add(new Scored(new DeckDto(
                d.Key, cards, d.Games,
                Math.Round(100.0 * d.Wins / d.Games, 1),
                ctx.Window.Sides == 0 ? 0 : Math.Round(100.0 * d.Games / ctx.Window.Sides, 2),
                avgElixir,
                cards.Select(c => c.Elixir).OrderBy(e => e).Take(4).Sum(),
                owned,
                ownedCards.Count == 0 ? 0 : Math.Round(ownedCards.Average(c => c.Level), 1),
                Math.Round(fit * 100, 1),
                GetMetaDecksUseCase.CopyLink(keys),
                ctx.Picks.GetValueOrDefault(d.Key)), keys, wilson, fit));
        }
        return list;
    }

    private static CardDto ToCard(int id, bool evo, Dictionary<int, CrCatalogCard> catalog, Dictionary<string, CrCard>? mine)
    {
        var c = catalog.GetValueOrDefault(id);
        var name = c?.Name ?? $"#{id}";
        var own = mine?.GetValueOrDefault(name);
        return new CardDto(id, name,
            (evo ? c?.EvoIconUrl : null) ?? c?.IconUrl ?? "",
            c?.IconUrl ?? "",
            evo, c?.ElixirCost ?? 0, c?.Rarity ?? "",
            own?.Level ?? 0, own?.MaxLevel ?? 0, own is not null, own is { EvolutionLevel: > 0 }, c?.HeroIconUrl, c?.EvoIconUrl);
    }

    /* ---------------- Выбор аналитика ---------------- */

    /// <summary>Разбор меты от нейросети (MetaAnalystUseCase) на языке игрока. Пусто - разбора ещё не было.</summary>
    private async Task<(Dictionary<string, PickDto> Picks, AnalystDto? Analyst)> AnalystAsync(string? lang, CancellationToken ct)
    {
        var raw = await settings.GetAsync(MetaAnalystUseCase.SettingKey, ct);
        if (string.IsNullOrEmpty(raw)) return ([], null);
        MetaAnalystUseCase.Report? report;
        try { report = JsonSerializer.Deserialize<MetaAnalystUseCase.Report>(raw); }
        catch (JsonException) { return ([], null); }
        if (report is null) return ([], null);

        var l = lang is "uk" or "en" ? lang : "ru";
        string Pick(Dictionary<string, string>? t) =>
            t is null ? "" : t.GetValueOrDefault(l) ?? t.GetValueOrDefault("ru") ?? t.Values.FirstOrDefault() ?? "";

        var picks = report.Picks
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new PickDto(g.First().Tag, Pick(g.First().Note)));
        return (picks, new AnalystDto(report.Day, Pick(report.Summary), picks.Count));
    }
}
