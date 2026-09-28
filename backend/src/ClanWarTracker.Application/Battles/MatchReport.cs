using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Разбор одного боя из того, что видно в журнале: колоды, уровни, башни, эликсир,
/// соперник - и своя история за месяц.
///
/// Реплеев Supercell не даёт, ходов не видно. Поэтому оценок за игру тут нет:
/// вывод строится только из фактов («утекло 17 эликсира», «у его башни 214 HP»),
/// а если причины не видно, так и говорим - «решилось в самом бою».
///
/// Чистая функция: ни базы, ни сети. Её же зовут и трекер (карточка захода), и
/// приложение (полный отчёт), поэтому карточка и отчёт не расходятся.
/// </summary>
public static class MatchReport
{
    /// <summary>Режимы, где уровни карт - твои собственные (ладдер). В турнирах и испытаниях уровни выравнены.</summary>
    public static readonly HashSet<int> LevelModes = [72000006, 72000268];

    /// <summary>Путь легенд: уровни тоже свои, но включаем после проверки данных на проде.</summary>
    public static readonly HashSet<int> PolModes = [72000450, 72000464];
    public static bool PolLevels { get; set; }

    /// <summary>Сглаживание процентов к своему обычному: «3 из 3» не должно перевешивать проверенное.</summary>
    public const int Prior = 8;

    public const int MinBaseGames = 20;
    public const int VsArchMinGames = 8;
    public const double VsArchMinDelta = 0.08;
    public const double BetterDeckMinShare = 0.15;
    public const int BetterDeckMinGames = 6;
    public const double BetterDeckMinDelta = 0.15;
    public const int DeckVsArchMinGames = 3;
    public const int UsualLeakMinGames = 10;
    public const int CloseHp = 700;
    public const double AfkLeak = 15;

    public record Arch(string Key, string Label, bool Other);
    public record Opponent(string? Name, string? Tag, string? Clan, int? Trophies, int? Diff, int? GlobalRank);
    public record Card(int Id, int Level, int Form);
    public record Decks(IReadOnlyList<Card> Mine, IReadOnlyList<Card> Theirs, Card? MyTower, Card? TheirTower);
    public record Tower(int? KingHp, IReadOnlyList<int>? PrincessHp);
    public record Levels(double MyAvg, double OppAvg, double Gap, int LowestMineId, int LowestMineLevel);
    public record Leak(double? Mine, double? Theirs, double? Usual);

    /// <param name="Code">afk, levels, close, leak, even, winLevels, winRank, winUpset, winClose.</param>
    /// <param name="Value">Главное число вывода: эликсир, разница уровней, HP, место, кубки.</param>
    /// <param name="Value2">Второе число: обычная утечка или уровень карты.</param>
    public record Verdict(string Code, double Value, double? Value2 = null, int? CardId = null);

    /// <param name="Index">Номер боя в заходе (с 1).</param>
    /// <param name="Strip">Результаты захода по порядку: W, L, D.</param>
    public record Session(DateTime StartUtc, DateTime EndUtc, int Index, int Count, int Wins, int Losses, int Draws, int Trophies, string Strip);

    /// <summary>Счёт против архетипа за месяц: сырой W–L и сглаженный процент против обычного.</summary>
    public record VsArch(int Wins, int Losses, int Games, double Percent, double BasePercent);

    /// <param name="CardId">Главная карта той колоды - по ней игрок её узнаёт.</param>
    public record BetterDeck(string DeckKey, int? CardId, int Wins, int Losses, int Games, double Percent, double CurrentPercent);

    public record DeckVsArch(int Wins, int Losses);
    public record Plus(VsArch? VsArch, BetterDeck? BetterDeck, DeckVsArch? DeckVsArch);

    public record Report(
        int Result,
        int CrownsFor,
        int CrownsAgainst,
        int? TrophyChange,
        string ModeKey,
        Opponent Opp,
        Arch? Arch,
        double? OppAvgElixir,
        Decks Decks,
        Tower? MyTowers,
        Tower? OppTowers,
        Levels? Levels,
        Leak? Leak,
        Verdict? Verdict,
        Session Session,
        Plus? Plus,
        int LockedCount,
        bool HasDetail);

    /// <summary>ladder, pol, war, trail, tourney или other - для фильтров и подписи режима.</summary>
    public static string ModeKey(string? type)
    {
        var t = type ?? "";
        if (t.Equals("PvP", StringComparison.OrdinalIgnoreCase)) return "ladder";
        if (t.Contains("pathOfLegend", StringComparison.OrdinalIgnoreCase)) return "pol";
        if (t.Contains("riverRace", StringComparison.OrdinalIgnoreCase)
            || t.Contains("boat", StringComparison.OrdinalIgnoreCase)
            || t.Contains("clanWar", StringComparison.OrdinalIgnoreCase)) return "war";
        if (t.Contains("trail", StringComparison.OrdinalIgnoreCase)) return "trail";
        if (t.Contains("tournament", StringComparison.OrdinalIgnoreCase)
            || t.Contains("challenge", StringComparison.OrdinalIgnoreCase)) return "tourney";
        return "other";
    }

    /// <summary>
    /// Уровни что-то значат, только когда колоды свои и не выравнены режимом. В
    /// турнире у всех 11-е, в драфте карты чужие: «соперник прокачан сильнее» там враньё.
    /// </summary>
    public static bool LevelsValid(int? gameModeId, string? deckSelection, MatchDetail? d)
    {
        if (d is null || gameModeId is not int gm) return false;
        if (!LevelModes.Contains(gm) && !(PolLevels && PolModes.Contains(gm))) return false;
        if (deckSelection != "collection") return false;
        var all = d.Me.Cards.Concat(d.Op.Cards).Select(c => c.Level).ToList();
        if (d.Me.Cards.Count != 8 || d.Op.Cards.Count != 8 || all.Any(l => l <= 0)) return false;
        // Все 16 одинаковые - почти наверняка выравненный режим, а не два одинаково прокачанных игрока.
        return all.Distinct().Count() > 1;
    }

    /// <summary>Средний уровень своих карт минус у соперника, до десятых. null - уровни не в счёт.</summary>
    public static double? LevelGap(int? gameModeId, string? deckSelection, MatchDetail? d)
    {
        if (!LevelsValid(gameModeId, deckSelection, d)) return null;
        return Math.Round(d!.Me.Cards.Average(c => c.Level) - d.Op.Cards.Average(c => c.Level), 1);
    }

    /// <summary>Самая слабая башня: наименьшее HP среди уцелевших. null - данных нет.</summary>
    public static int? Weakest(MatchSide s)
    {
        var hp = new List<int>(s.PrincessHp ?? Array.Empty<int>());
        if (s.KingHp is int k) hp.Add(k);
        hp.RemoveAll(x => x <= 0);
        return hp.Count == 0 ? null : hp.Min();
    }

    public static string ArchOf(PlayerBattle b, IReadOnlyDictionary<int, CrCatalogCard> catalog) =>
        b.OppArchetype ?? Archetypes.ClassifyKey(b.OppDeckKey, catalog) ?? "";

    /// <param name="month">Бои этого игрока за 30 дней по возрастанию времени, включая <paramref name="b"/>.</param>
    /// <param name="full">Показывать строки Плюса. Иначе они только считаются в <see cref="Report.LockedCount"/>.</param>
    public static Report Build(
        PlayerBattle b, MatchDetail? d, IReadOnlyList<PlayerBattle> month, bool full,
        IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var archCache = new Dictionary<int, string>();
        string ArchFor(PlayerBattle r)
        {
            if (r.Id != 0 && archCache.TryGetValue(r.Id, out var a)) return a;
            a = ArchOf(r, catalog);
            if (r.Id != 0) archCache[r.Id] = a;
            return a;
        }

        var archKey = ArchFor(b);
        var arch = archKey == "" ? null : new Arch(archKey, Archetypes.Label(archKey, catalog), Archetypes.IsOther(archKey));
        var mode = ModeKey(b.Type);
        var prior = month.Where(r => r.BattleTimeUtc < b.BattleTimeUtc).ToList();

        var opp = new Opponent(
            b.OppName, b.OppTag, d?.Op.ClanName, d?.Op.StartingTrophies,
            d?.Op.StartingTrophies is int ot && d.Me.StartingTrophies is int mt ? ot - mt : null,
            d?.Op.GlobalRank);

        var oppKeys = MetaCard.ParseDeckKey(b.OppDeckKey);
        var costs = oppKeys.Select(k => catalog.GetValueOrDefault(Math.Abs(k))?.ElixirCost ?? 0).ToList();
        double? oppAvg = costs.Count == 8 && costs.All(c => c > 0) ? Math.Round(costs.Average(), 1) : null;

        var decks = d is null
            ? new Decks(FromKey(b.DeckKey), FromKey(b.OppDeckKey), null, null)
            : new Decks(
                d.Me.Cards.Select(c => new Card(c.Id, c.Level, c.Form)).ToList(),
                d.Op.Cards.Select(c => new Card(c.Id, c.Level, c.Form)).ToList(),
                d.Me.TowerTroop is { } mtt ? new Card(mtt.Id, mtt.Level, 0) : null,
                d.Op.TowerTroop is { } ott ? new Card(ott.Id, ott.Level, 0) : null);

        Levels? levels = null;
        if (LevelsValid(b.GameModeId, b.DeckSelection, d))
        {
            var mine = d!.Me.Cards;
            var myAvg = mine.Average(c => c.Level);
            var oppAvgLvl = d.Op.Cards.Average(c => c.Level);
            var lowest = mine.OrderBy(c => c.Level).First();
            levels = new Levels(Math.Round(myAvg, 1), Math.Round(oppAvgLvl, 1), Math.Round(myAvg - oppAvgLvl, 1),
                lowest.Id, lowest.Level);
        }

        var usualRows = prior.Where(r => r.GameModeId == b.GameModeId && r.ElixirLeaked is not null).ToList();
        double? usual = b.GameModeId is not null && usualRows.Count >= UsualLeakMinGames
            ? Math.Round(usualRows.Average(r => r.ElixirLeaked!.Value), 1) : null;
        var myLeak = b.ElixirLeaked ?? d?.Me.Leak;
        var leak = myLeak is null && d?.Op.Leak is null ? null
            : new Leak(myLeak is double ml ? Math.Round(ml, 1) : null, d?.Op.Leak is double tl ? Math.Round(tl, 1) : null, usual);

        var verdict = VerdictFor(b, d, mode, levels, myLeak, usual);
        var session = SessionFor(month, b);

        var plus = PlusFor(b, prior, archKey, ArchFor, catalog);
        var locked = 0;
        if (!full && plus is not null)
        {
            locked = (plus.VsArch is null ? 0 : 1) + (plus.BetterDeck is null ? 0 : 1);
            plus = null;
        }

        return new Report(
            b.Result, b.CrownsFor, b.CrownsAgainst, b.TrophyChange, mode, opp, arch, oppAvg, decks,
            d is null ? null : new Tower(d.Me.KingHp, d.Me.PrincessHp),
            d is null ? null : new Tower(d.Op.KingHp, d.Op.PrincessHp),
            levels, leak, verdict, session, plus, locked, d is not null);
    }

    private static List<Card> FromKey(string deckKey) =>
        MetaCard.ParseDeckKey(deckKey).Select(k => new Card(Math.Abs(k), 0, k < 0 ? 1 : 0)).ToList();

    /// <summary>
    /// Короткий вывод - первое подходящее правило. Только из бесплатных фактов: Плюс
    /// добавляет строки, но вывод не меняет.
    /// </summary>
    public static Verdict? VerdictFor(PlayerBattle b, MatchDetail? d, string mode, Levels? levels, double? myLeak, double? usual)
    {
        if (b.Result < 0)
        {
            if (myLeak is double l && l >= AfkLeak) return new Verdict("afk", Math.Round(l, 1));
            if (levels is { Gap: <= -1.0 })
                return new Verdict("levels", levels.Gap, levels.LowestMineLevel, levels.LowestMineId);
            if (d is not null && b.CrownsAgainst - b.CrownsFor == 1 && Weakest(d.Op) is int hp && hp <= CloseHp)
                return new Verdict("close", hp);
            if (usual is double u && myLeak is double l2 && l2 >= Math.Max(4, 2 * u))
                return new Verdict("leak", Math.Round(l2, 1), u);
            if (levels is not null && Math.Abs(levels.Gap) < 0.5) return new Verdict("even", levels.Gap);
            return null;
        }
        if (b.Result > 0)
        {
            if (levels is { Gap: <= -1.0 }) return new Verdict("winLevels", levels.Gap);
            if (d?.Op.GlobalRank is int rank and > 0) return new Verdict("winRank", rank);
            if (mode == "ladder" && d?.Op.StartingTrophies is int ot && d.Me.StartingTrophies is int mt && ot - mt >= 200)
                return new Verdict("winUpset", ot - mt);
            if (d is not null && Weakest(d.Me) is int hp && hp <= CloseHp) return new Verdict("winClose", hp);
        }
        return null;
    }

    public static Session SessionFor(IReadOnlyList<PlayerBattle> month, PlayerBattle b)
    {
        var s = BattleAnalyzer.SessionAround(month, b.BattleTimeUtc);
        if (s.Count == 0) s = [b];
        var index = s.FindIndex(x => x.BattleTimeUtc == b.BattleTimeUtc) + 1;
        return new Session(
            s[0].BattleTimeUtc, s[^1].BattleTimeUtc, Math.Max(1, index), s.Count,
            s.Count(x => x.Result > 0), s.Count(x => x.Result < 0), s.Count(x => x.Result == 0),
            s.Sum(x => x.TrophyChange ?? 0),
            new string(s.Select(x => x.Result > 0 ? 'W' : x.Result < 0 ? 'L' : 'D').ToArray()));
    }

    /// <summary>Сглаженный процент: (w + 8·base)/(n + 8).</summary>
    public static double Shrunk(int wins, int games, double baseRate) => (wins + Prior * baseRate) / (games + Prior);

    /// <summary>
    /// Строки Плюса: «против этого архетипа за месяц» и «чем против него лучше играть».
    /// Считаются только по своим колодам (collection): в драфте и испытаниях колода
    /// выдана режимом, и её процент ничего не говорит о выборе игрока.
    /// </summary>
    private static Plus? PlusFor(
        PlayerBattle b, List<PlayerBattle> prior, string archKey, Func<PlayerBattle, string> archFor,
        IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        if (archKey == "") return null;
        var own = prior.Where(r => r.DeckSelection is null or "collection").ToList();
        if (own.Count < MinBaseGames) return null;
        var baseRate = (double)own.Count(r => r.Result > 0) / own.Count;

        var vs = own.Where(r => archFor(r) == archKey).ToList();
        int vw = vs.Count(r => r.Result > 0), vl = vs.Count(r => r.Result < 0);

        VsArch? vsArch = null;
        if (vs.Count >= VsArchMinGames)
        {
            var shrunk = Shrunk(vw, vs.Count, baseRate);
            if (Math.Abs(shrunk - baseRate) >= VsArchMinDelta)
                vsArch = new VsArch(vw, vl, vs.Count, Math.Round(shrunk * 100), Math.Round(baseRate * 100));
        }

        BetterDeck? better = null;
        if (b.Result < 0 && (double)vs.Count / own.Count >= BetterDeckMinShare)
        {
            var current = vs.Where(r => r.DeckKey == b.DeckKey).ToList();
            var currentRate = Shrunk(current.Count(r => r.Result > 0), current.Count, baseRate);
            var best = vs
                .Where(r => r.DeckKey != b.DeckKey)
                .GroupBy(r => r.DeckKey)
                .Select(g => (Key: g.Key, Games: g.Count(), Wins: g.Count(r => r.Result > 0), Losses: g.Count(r => r.Result < 0)))
                .Where(g => g.Games >= BetterDeckMinGames)
                .Select(g => (g.Key, g.Games, g.Wins, g.Losses, Rate: Shrunk(g.Wins, g.Games, baseRate)))
                .OrderByDescending(g => g.Rate)
                .FirstOrDefault();
            if (best.Key is not null && best.Rate - currentRate >= BetterDeckMinDelta)
                better = new BetterDeck(best.Key, SignatureCard(best.Key, catalog), best.Wins, best.Losses, best.Games,
                    Math.Round(best.Rate * 100), Math.Round(currentRate * 100));
        }

        var thisDeck = vs.Where(r => r.DeckKey == b.DeckKey).ToList();
        var deckVs = thisDeck.Count >= DeckVsArchMinGames
            ? new DeckVsArch(thisDeck.Count(r => r.Result > 0), thisDeck.Count(r => r.Result < 0))
            : null;

        return vsArch is null && better is null && deckVs is null ? null : new Plus(vsArch, better, deckVs);
    }

    /// <summary>Карта, по которой игрок узнаёт свою колоду: главная карта архетипа или самая дорогая.</summary>
    public static int? SignatureCard(string deckKey, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var key = Archetypes.ClassifyKey(deckKey, catalog);
        return key is null ? null : Archetypes.KeyCard(key, catalog);
    }
}
