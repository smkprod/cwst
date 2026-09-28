using System.Globalization;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Тексты трекера боёв: карточка захода, итог захода и строки разбора.
///
/// Одни и те же строки идут и в бота, и в приложение (сервер отдаёт их готовыми),
/// поэтому вывод по бою везде звучит одинаково.
/// </summary>
public static class TrackerText
{
    /// <summary>Сколько последних боёв показывает полоска в карточке.</summary>
    public const int StripLength = 10;

    public static string ResultMark(int result) => result > 0 ? "✅" : result < 0 ? "❌" : "➖";

    public static string Strip(IEnumerable<PlayerBattle> battles) =>
        string.Concat(battles.Select(b => ResultMark(b.Result)));

    public static string Num(double value) =>
        Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    public static string SignedNum(double value) =>
        value > 0 ? "+" + Num(value) : value < 0 ? "−" + Num(-value) : "0";

    public static string ArchLabel(BotText t, MatchReport.Arch? arch) =>
        arch is null ? "?" : arch.Other ? string.Format(t.TrkOther, arch.Label) : arch.Label;

    public static string CardName(IReadOnlyDictionary<int, CrCatalogCard> catalog, int? id) =>
        id is int i && catalog.GetValueOrDefault(i) is { } c ? c.Name : "?";

    /// <summary>Короткий вывод по бою. null - вывода нет (причина не видна или ничья).</summary>
    public static string? Verdict(BotText t, MatchReport.Verdict? v, IReadOnlyDictionary<int, CrCatalogCard> catalog) => v?.Code switch
    {
        "afk" => string.Format(t.TrkVAfk, Num(v.Value)),
        "levels" => string.Format(t.TrkVLevels, Num(-v.Value), CardName(catalog, v.CardId), (int)(v.Value2 ?? 0)),
        "close" => string.Format(t.TrkVClose, (int)v.Value),
        "leak" => string.Format(t.TrkVLeak, Num(v.Value), Num(v.Value2 ?? 0)),
        "even" => t.TrkVEven,
        "winLevels" => string.Format(t.TrkVWinLevels, SignedNum(v.Value)),
        "winRank" => string.Format(t.TrkVWinRank, (int)v.Value),
        "winUpset" => string.Format(t.TrkVWinUpset, (int)v.Value),
        "winClose" => string.Format(t.TrkVWinClose, (int)v.Value),
        _ => null,
    };

    /// <summary>«Против Golem за месяц 3–8 (обычно ты 54%)» - без значка: его ставит вызывающий (📈 или 🎁).</summary>
    public static string VsArch(BotText t, MatchReport.Arch? arch, MatchReport.VsArch p) =>
        string.Format(t.TrkPVsArch, ArchLabel(t, arch), p.Wins, p.Losses, p.BasePercent.ToString("0", CultureInfo.InvariantCulture));

    public static string BetterDeck(
        BotText t, MatchReport.Arch? arch, MatchReport.BetterDeck p, IReadOnlyDictionary<int, CrCatalogCard> catalog) =>
        string.Format(t.TrkPBetterDeck, ArchLabel(t, arch), CardName(catalog, p.CardId), p.Wins, p.Losses);

    /// <summary>
    /// Одна строка Плюса для карточки: «чем играть» важнее «против кого», но в серии
    /// поражений совет сменить колоду не даём - он звучит как «играй дальше».
    /// </summary>
    public static string? PlusLine(
        BotText t, MatchReport.Report r, bool inStreak, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        if (r.Plus is not { } p) return null;
        if (p.BetterDeck is { } bd && !inStreak) return BetterDeck(t, r.Arch, bd, catalog);
        if (p.VsArch is { } va) return VsArch(t, r.Arch, va);
        return null;
    }

    public static string Trophies(int? change) =>
        change is int c ? TiltMessages.Signed(c) : "±0";

    /// <summary>Строка боя над тильт-сигналом: «Бой 4: 0–1 против Golem · −29🏆».</summary>
    public static string AlertPrefix(BotText t, MatchReport.Report r) =>
        string.Format(t.TrkAlertPrefix, r.Session.Index, r.CrownsFor, r.CrownsAgainst, ArchLabel(t, r.Arch), Trophies(r.TrophyChange));

    /// <param name="session">Бои захода по порядку, последний - тот, о котором карточка.</param>
    /// <param name="plusLine">Строка Плюса уже со значком (📈 или 🎁) или null.</param>
    public static string Card(
        BotText t, MatchReport.Report r, IReadOnlyList<PlayerBattle> session, int tz,
        IReadOnlyDictionary<int, CrCatalogCard> catalog, string? plusLine, DateTime? pauseUntilUtc)
    {
        var lines = new List<string>
        {
            string.Format(t.TrkCardHead, session[0].BattleTimeUtc.AddMinutes(tz).ToString("HH:mm"),
                session.Count(b => b.Result > 0), session.Count(b => b.Result < 0),
                Trophies(session.Sum(b => b.TrophyChange ?? 0))),
            Strip(session.TakeLast(StripLength)),
            string.Format(t.TrkLast, ResultMark(r.Result), r.CrownsFor, r.CrownsAgainst, Trophies(r.TrophyChange),
                r.Opp.Name ?? "?", ArchLabel(t, r.Arch)),
        };
        if (Verdict(t, r.Verdict, catalog) is { } verdict) lines.Add(verdict);
        if (plusLine is not null) lines.Add(plusLine);
        if (pauseUntilUtc is DateTime pause) lines.Add(string.Format(t.TrkTiltPause, pause.AddMinutes(tz).ToString("HH:mm")));
        return string.Join("\n", lines);
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> CardButtons(BotText t, int battleId) =>
    [
        [
            new BotButton(t.TrkBtnReport, Url: $"startapp:m_{battleId}"),
            new BotButton(t.TrkBtnAll, Url: "startapp:matches"),
        ],
        [new BotButton(t.TrkBtnMute, "trk|m")],
    ];

    public static IReadOnlyList<IReadOnlyList<BotButton>> SummaryButtons(BotText t, bool upsell)
    {
        var row = new List<BotButton> { new(t.TrkBtnSession, Url: "startapp:matches") };
        if (upsell) row.Add(new(t.TrkBtnPlus, Url: "startapp:plus_trk"));
        return [row];
    }

    /// <param name="Upsell">В итоге есть строка продажи - под ним кнопка Плюса.</param>
    public record SummaryText(string Text, bool Upsell);

    /// <param name="lockedCount">Сколько подсказок Плюса было скрыто за заход.</param>
    /// <param name="moment">Когда Стоп-тильт написал бы и что было потом, или null.</param>
    public static SummaryText Summary(
        BotText t, IReadOnlyList<PlayerBattle> session, int tz, bool paid,
        IReadOnlyDictionary<int, CrCatalogCard> catalog, int lockedCount,
        (int AfterIndex, int Wins, int Losses, int Trophies)? moment)
    {
        var lines = new List<string>
        {
            string.Format(t.TrkSumHead,
                session[0].BattleTimeUtc.AddMinutes(tz).ToString("HH:mm"),
                session[^1].BattleTimeUtc.AddMinutes(tz).ToString("HH:mm"),
                session.Count(b => b.Result > 0), session.Count(b => b.Result < 0),
                Trophies(session.Sum(b => b.TrophyChange ?? 0))),
            Strip(session),
        };

        // Лучший бой - победа над самым сильным соперником (по кубкам), иначе самая крупная.
        var best = session
            .Where(b => b.Result > 0)
            .Select(b => (Battle: b, Opp: MatchDetailCodec.Decode(b.DetailJson)?.Op.StartingTrophies ?? 0))
            .OrderByDescending(x => x.Opp)
            .ThenByDescending(x => x.Battle.TrophyChange ?? 0)
            .ThenByDescending(x => x.Battle.CrownsFor - x.Battle.CrownsAgainst)
            .Select(x => x.Battle)
            .FirstOrDefault();
        if (best is not null)
        {
            var arch = Arch(best, catalog);
            lines.Add(string.Format(t.TrkSumBest, best.CrownsFor, best.CrownsAgainst,
                best.OppName ?? ArchLabel(t, arch), Trophies(best.TrophyChange)));
        }

        var worst = session
            .GroupBy(b => MatchReport.ArchOf(b, catalog))
            .Where(g => g.Key != "" && g.Count(b => b.Result < 0) >= 2)
            .OrderByDescending(g => g.Count(b => b.Result < 0))
            .FirstOrDefault();
        if (worst is not null)
            lines.Add(string.Format(t.TrkSumWorst, ArchLabel(t, Arch(worst.First(), catalog)),
                worst.Count(b => b.Result > 0), worst.Count(b => b.Result < 0)));

        var wonGaps = session.Where(b => b.Result > 0 && b.LevelGap is not null).Select(b => b.LevelGap!.Value).ToList();
        var lostGaps = session.Where(b => b.Result < 0 && b.LevelGap is not null).Select(b => b.LevelGap!.Value).ToList();
        if (wonGaps.Count >= 2 && lostGaps.Count >= 2)
            lines.Add(string.Format(t.TrkSumLevels, SignedNum(lostGaps.Average()), SignedNum(wonGaps.Average())));

        // Продаём не больше одной строкой и только тем, у кого Плюса нет.
        var upsell = false;
        if (!paid)
        {
            if (moment is { } m)
            {
                lines.Add(string.Format(t.TrkSumMoment, m.AfterIndex, m.Wins, m.Losses, Trophies(m.Trophies)));
                upsell = true;
            }
            else if (lockedCount >= 2)
            {
                lines.Add(string.Format(t.TrkSumLocked, lockedCount));
                upsell = true;
            }
        }
        return new SummaryText(string.Join("\n", lines), upsell);
    }

    private static MatchReport.Arch? Arch(PlayerBattle b, IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var key = MatchReport.ArchOf(b, catalog);
        return key == "" ? null : new MatchReport.Arch(key, Archetypes.Label(key, catalog), Archetypes.IsOther(key));
    }

    /// <summary>
    /// Когда Стоп-тильт написал бы в этом заходе и что было потом. Только если после
    /// момента сыграно не меньше двух боёв: иначе «дальше было 0–1» ничего не доказывает.
    /// </summary>
    public static (int AfterIndex, int Wins, int Losses, int Trophies)? Moment(IReadOnlyList<PlayerBattle> session, int threshold)
    {
        var eligible = session.Where(b => BattleAnalyzer.IsTiltEligible(b.Type)).ToList();
        if (eligible.Count == 0) return null;
        var m = BattleAnalyzer.Moments(eligible, threshold, eligible[0].BattleTimeUtc).FirstOrDefault();
        if (m is null) return null;
        var after = eligible.Where(b => b.BattleTimeUtc > m.AtUtc).ToList();
        if (after.Count < 2) return null;
        var index = session.ToList().FindIndex(b => b.BattleTimeUtc == m.AtUtc) + 1;
        return (index, after.Count(b => b.Result > 0), after.Count(b => b.Result < 0), after.Sum(b => b.TrophyChange ?? 0));
    }
}
