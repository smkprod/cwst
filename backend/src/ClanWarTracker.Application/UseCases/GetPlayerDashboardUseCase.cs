using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Профиль-дашборд: всё о своей игре на одном экране - кубки и их путь, счёт, серии,
/// последние бои, режимы, короны, текущая колода, какие карты тянут, и главное -
/// «что не так, почему и как исправить» с фишкой из библиотеки.
///
/// Бесплатно - вся картина и первый вывод целиком: человек должен увидеть, что бот
/// понимает его игру. Остальные выводы и проценты по картам - в Плюсе: цифры
/// видны, а ответ «как исправить» - за ним.
/// </summary>
public class GetPlayerDashboardUseCase(
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IClashRoyaleApi crApi,
    CollectPlayerBattlesUseCase collect,
    IMetaRepository meta,
    PlusAccess plus)
{
    public const int RecentShown = 20;
    public const int TrophyPoints = 40;
    public const int CardRatesShown = 12;
    public const int CardRatesFree = 3;
    public const int InsightsFree = 1;
    private const int MinCardGames = 3;

    public record BattleTileDto(int Id, int Result, int CrownsFor, int CrownsAgainst, string Mode, string AtUtc,
        string? OppArchetype, int? Trophies);

    public record ModeRowDto(string Mode, int Games, int Wins, double WinPercent);

    public record CrownsDto(int Wins1, int Wins2, int Wins3, int Losses, int Draws);

    public record DeckCardDto(string Name, string IconUrl, int Level, int MaxLevel, bool Evo, int Elixir);

    public record CardRateDto(MetaCardDto Card, int Games, double WinPercent);

    /// <summary>
    /// Вывод «что не так»: данные, а не готовый текст - фразы на трёх языках собирает
    /// приложение. Tip - фишка из библиотеки, которая показывает, как исправить.
    /// </summary>
    /// <param name="Kind">tough_card, leak, tilt, deck_meta, daypart, levels.</param>
    /// <param name="Value">Главное число вывода (процент побед, утечка, разница уровней).</param>
    /// <param name="Baseline">С чем сравниваем (обычный процент, утечка в победах, процент топа).</param>
    /// <param name="Key">Карта соперника, часть суток или колода - по виду вывода.</param>
    public record InsightDto(string Kind, string? Tip, string? Key, string? IconUrl, int Games, double Value, double Baseline);

    public record DashboardDto(
        string Name, string Tag, int Trophies, int BestTrophies, int TrophyDelta, List<int> TrophySeries,
        int Games, int Wins, int Losses, int Draws, double WinPercent,
        int CurrentStreak, int BestWinStreak, int WorstLossStreak,
        List<BattleTileDto> Recent, List<ModeRowDto> Modes, CrownsDto Crowns,
        List<DeckCardDto> Deck, double AvgElixir,
        List<CardRateDto> CardRates, int CardRatesLocked,
        List<InsightDto> Insights, int InsightsLocked,
        bool Unlocked, bool Paywall, string? SinceUtc);

    /// <returns>null - игрок не привязан.</returns>
    public async Task<DashboardDto?> ExecuteAsync(long telegramUserId, int tzOffsetMinutes, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return null;
        var tag = player.PlayerTag;

        try { await collect.SyncAsync(tag, ct); }
        catch { /* API игры недоступен - покажем накопленное */ }

        var list = await battles.GetSinceAsync(tag, DateTime.UtcNow - CollectPlayerBattlesUseCase.Keep, ct);
        CrPlayerInfo? info = null;
        try { info = await crApi.GetPlayerInfoAsync(tag, ct); }
        catch { /* без профиля - без кубков и колоды, остальное есть */ }
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var access = await plus.GetAsync(telegramUserId, ct);

        var totals = BattleAnalyzer.Count(list);
        var (bestWin, worstLoss) = Streaks(list);
        var current = info?.CurrentWinLoseStreak ?? CurrentStreak(list);

        var recent = list
            .OrderByDescending(b => b.BattleTimeUtc)
            .Take(RecentShown)
            .Select(b => new BattleTileDto(b.Id, Math.Sign(b.Result), b.CrownsFor, b.CrownsAgainst,
                MatchReport.ModeKey(b.Type), b.BattleTimeUtc.ToString("O"),
                b.OppArchetype is { Length: > 0 } a ? Archetypes.Label(a, catalog) : null, b.TrophyChange))
            .ToList();

        var modes = list
            .GroupBy(b => MatchReport.ModeKey(b.Type))
            .Select(g => new ModeRowDto(g.Key, g.Count(), g.Count(b => b.Result > 0),
                BattleAnalyzer.Percent(g.Count(b => b.Result > 0), g.Count())))
            .OrderByDescending(m => m.Games)
            .ToList();

        var crowns = new CrownsDto(
            list.Count(b => b.Result > 0 && b.CrownsFor <= 1),
            list.Count(b => b.Result > 0 && b.CrownsFor == 2),
            list.Count(b => b.Result > 0 && b.CrownsFor >= 3),
            totals.Losses, totals.Draws);

        var deck = (info?.CurrentDeck ?? [])
            .Select(c => new DeckCardDto(c.Name, c.EvolutionLevel > 0 && c.EvoIconUrl is { } evo ? evo : c.IconUrl,
                c.Level, c.MaxLevel, c.EvolutionLevel > 0, c.ElixirCost ?? catalog.GetValueOrDefault(c.Id)?.ElixirCost ?? 0))
            .ToList();
        var avgElixir = deck.Count == 0 ? 0 : Math.Round(deck.Average(c => c.Elixir), 1);

        var rates = CardRates(list)
            .Select(r => new CardRateDto(GetMetaDecksUseCase.Card(r.Key, catalog), r.Games, BattleAnalyzer.Percent(r.Wins, r.Games)))
            .Take(CardRatesShown)
            .ToList();

        var window = await MetaWindow.LoadAsync(meta, ct);
        var insights = Insights(list, tzOffsetMinutes, catalog, window);

        // Без Плюса режем на сервере: спрятанное на клиенте всё равно уехало бы в ответе
        int ratesLocked = 0, insightsLocked = 0;
        if (!access.Unlocked)
        {
            ratesLocked = Math.Max(0, rates.Count - CardRatesFree);
            rates = rates.Take(CardRatesFree).ToList();
            insightsLocked = Math.Max(0, insights.Count - InsightsFree);
            // Закрытые выводы - без цифр и карт: остаётся только вид, чтобы показать «что найдено»
            insights = insights.Take(InsightsFree)
                .Concat(insights.Skip(InsightsFree).Select(i => new InsightDto(i.Kind, null, null, null, 0, 0, 0)))
                .ToList();
        }

        return new DashboardDto(
            info?.Name ?? player.Name, tag, info?.Trophies ?? 0, info?.BestTrophies ?? 0,
            list.Where(b => b.BattleTimeUtc >= DateTime.UtcNow.AddDays(-7)).Sum(b => b.TrophyChange ?? 0),
            TrophySeries(list, info?.Trophies),
            totals.Games, totals.Wins, totals.Losses, totals.Draws, BattleAnalyzer.Percent(totals.Wins, totals.Games),
            current, bestWin, worstLoss,
            recent, modes, crowns, deck, avgElixir,
            rates, ratesLocked, insights, insightsLocked,
            access.Unlocked, access.Paywall,
            list.Count == 0 ? null : list.Min(b => b.BattleTimeUtc).ToString("O"));
    }

    /// <summary>Лучшая серия побед и худшая серия поражений подряд. Ничья серию не рвёт.</summary>
    public static (int BestWin, int WorstLoss) Streaks(IEnumerable<PlayerBattle> list)
    {
        int win = 0, loss = 0, bestWin = 0, worstLoss = 0;
        foreach (var b in list.OrderBy(b => b.BattleTimeUtc))
        {
            if (b.Result > 0) { win++; loss = 0; bestWin = Math.Max(bestWin, win); }
            else if (b.Result < 0) { loss++; win = 0; worstLoss = Math.Max(worstLoss, loss); }
        }
        return (bestWin, worstLoss);
    }

    /// <summary>Текущая серия по журналу: плюс - победы подряд, минус - поражения.</summary>
    public static int CurrentStreak(IEnumerable<PlayerBattle> list)
    {
        var s = 0;
        foreach (var b in list.OrderByDescending(b => b.BattleTimeUtc))
        {
            if (b.Result == 0) continue;
            if (s == 0) s = b.Result > 0 ? 1 : -1;
            else if ((s > 0) == (b.Result > 0)) s += s > 0 ? 1 : -1;
            else break;
        }
        return s;
    }

    /// <summary>
    /// Путь кубков по ладдер-боям: от нынешних кубков назад по изменениям. null или нет
    /// изменений - пусто (график не рисуем, чем рисовать неправду).
    /// </summary>
    public static List<int> TrophySeries(IEnumerable<PlayerBattle> list, int? trophiesNow)
    {
        if (trophiesNow is not int now || now <= 0) return [];
        var ladder = list.Where(b => MatchReport.ModeKey(b.Type) == "ladder" && b.TrophyChange is not null)
            .OrderByDescending(b => b.BattleTimeUtc)
            .Take(TrophyPoints)
            .ToList();
        if (ladder.Count < 2) return [];
        var series = new List<int> { now };
        var t = now;
        foreach (var b in ladder)
        {
            t -= b.TrophyChange!.Value;
            series.Add(t);
        }
        series.Reverse();
        return series;
    }

    public record CardRate(int Key, int Games, int Wins);

    /// <summary>Процент побед со своей картой в колоде. Эволюцию считаем той же картой.</summary>
    public static List<CardRate> CardRates(IEnumerable<PlayerBattle> list)
    {
        var by = new Dictionary<int, (int Games, int Wins)>();
        foreach (var b in list.Where(b => b.Result != 0))
            foreach (var k in MetaCard.ParseDeckKey(b.DeckKey).Select(Math.Abs).Distinct())
            {
                by.TryGetValue(k, out var v);
                by[k] = (v.Games + 1, v.Wins + (b.Result > 0 ? 1 : 0));
            }
        return by.Where(kv => kv.Value.Games >= MinCardGames)
            .Select(kv => new CardRate(kv.Key, kv.Value.Games, kv.Value.Wins))
            .OrderByDescending(r => r.Games)
            .ThenByDescending(r => (double)r.Wins / r.Games)
            .ToList();
    }

    /// <summary>
    /// Выводы «что не так», самые весомые первыми. Каждый - с числом из боёв игрока и
    /// фишкой, которая показывает исправление. Шум отсекаем порогами: лучше два честных
    /// вывода, чем шесть случайных.
    /// </summary>
    public static List<InsightDto> Insights(IReadOnlyList<PlayerBattle> list, int tz,
        Dictionary<int, CrCatalogCard> catalog, MetaWindow.Data? window)
    {
        var found = new List<(double Weight, InsightDto Dto)>();
        var decided = list.Where(b => b.Result != 0).ToList();
        if (decided.Count < 5) return [];
        var baseline = BattleAnalyzer.Percent(decided.Count(b => b.Result > 0), decided.Count);

        foreach (var t in BattleAnalyzer.ToughCards(list, take: 3))
        {
            var card = GetMetaDecksUseCase.Card(t.CardKey, catalog);
            var wp = BattleAnalyzer.Percent(t.Wins, t.Games);
            if (baseline - wp < 10) continue;
            found.Add(((baseline - wp) * Math.Sqrt(t.Games), new InsightDto(
                "tough_card", TipFor(card.Name), card.Name, card.IconUrl, t.Games, wp, baseline)));
        }

        if (BattleAnalyzer.ElixirLeak(list) is { } leak && leak.Games >= 5 && leak.Losses >= 1.5 && leak.Losses - leak.Wins >= 0.5)
            found.Add((leak.Losses * 12, new InsightDto("leak", "elixir", null, null, leak.Games,
                Math.Round(leak.Losses, 1), Math.Round(leak.Wins, 1))));

        var tilt = BattleAnalyzer.Tilt(list);
        if (tilt.AfterTwoLosses >= 4)
        {
            var after2 = BattleAnalyzer.Percent(tilt.AfterTwoLossesWins, tilt.AfterTwoLosses);
            if (baseline - after2 >= 10)
                found.Add(((baseline - after2) * Math.Sqrt(tilt.AfterTwoLosses), new InsightDto(
                    "tilt", "tilt", null, null, tilt.AfterTwoLosses, after2, baseline)));
        }

        var worstPart = BattleAnalyzer.DayParts(list, tz)
            .Where(s => s.Games >= 6)
            .Select(s => (s.Key, s.Games, Wp: BattleAnalyzer.Percent(s.Wins, s.Games)))
            .OrderBy(s => s.Wp)
            .FirstOrDefault();
        if (worstPart.Key is not null && baseline - worstPart.Wp >= 12)
            found.Add(((baseline - worstPart.Wp) * Math.Sqrt(worstPart.Games) * 0.7, new InsightDto(
                "daypart", "tilt", worstPart.Key, null, worstPart.Games, worstPart.Wp, baseline)));

        var gaps = decided.Where(b => b.Result < 0 && b.LevelGap is not null).Select(b => b.LevelGap!.Value).ToList();
        if (gaps.Count >= 5 && gaps.Average() <= -0.6)
            found.Add((-gaps.Average() * 20, new InsightDto("levels", "levels", null, null, gaps.Count,
                Math.Round(gaps.Average(), 1), 0)));

        if (window is not null)
        {
            var main = BattleAnalyzer.Decks(list).FirstOrDefault(d => d.Games >= 8);
            if (main is not null && BattleAnalyzer.ClosestMetaDeck(MetaCard.ParseDeckKey(main.Key), window.Decks) is { } close
                && close.Shared >= 7 && close.Games >= 30)
            {
                var mine = BattleAnalyzer.Percent(main.Wins, main.Games);
                var top = BattleAnalyzer.Percent(close.Wins, close.Games);
                if (top - mine >= 8)
                    found.Add(((top - mine) * Math.Sqrt(main.Games) * 0.8, new InsightDto(
                        "deck_meta", "deck", null, null, main.Games, mine, top)));
            }
        }

        return found.OrderByDescending(f => f.Weight).Select(f => f.Dto).Take(5).ToList();
    }

    /// <summary>Фишка из библиотеки против карты соперника. null - общей фишки нет.</summary>
    public static string? TipFor(string cardName) => cardName switch
    {
        "Hog Rider" or "Royal Hogs" or "Ram Rider" or "Battle Ram" or "Wall Breakers" => "anti_hog",
        "Giant" or "Golem" or "Electro Giant" or "Goblin Giant" or "Royal Giant" or "Elixir Golem" => "anti_tank",
        "Mega Knight" or "P.E.K.K.A" or "Prince" or "Dark Prince" or "Elite Barbarians" or "Sparky" or "Lumberjack" => "kite",
        "Goblin Barrel" or "Skeleton Barrel" or "Goblin Gang" or "Princess" => "log_barrel",
        "Balloon" or "Lava Hound" or "Minion Horde" or "Baby Dragon" or "Inferno Dragon" => "anti_air",
        "X-Bow" or "Mortar" => "anti_siege",
        "Miner" or "Graveyard" or "Goblin Drill" => "anti_miner",
        _ => null,
    };
}
