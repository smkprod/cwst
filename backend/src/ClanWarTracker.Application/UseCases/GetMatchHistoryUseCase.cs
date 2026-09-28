using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// История боёв: заходы и бои в них, сверху - «кому ты проигрываешь» и уровни.
///
/// Работает у всех привязанных, даже без трекера: бои копятся и так. Без Плюса
/// видно 7 дней, с Плюсом - 30. Кончился Плюс - ничего не удаляется, просто
/// старое снова под замком.
/// </summary>
public class GetMatchHistoryUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    CollectPlayerBattlesUseCase collect,
    PlusAccess plus,
    TrackerActionsUseCase tracker)
{
    public const int FreeDays = 7;
    public const int PageSize = 50;

    /// <summary>Меньше встреч с архетипом - случайность, а не «кому проигрываешь».</summary>
    public const int ArchMinGames = 5;
    public const int LevelAvgMinGames = 5;
    public const int LevelPctMinGames = 8;

    /// <returns>null - игрок не привязан.</returns>
    public async Task<MatchHistoryDto?> ExecuteAsync(
        long tg, DateTime? before, string? result, string? mode, string? arch, string? lang, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return null;

        var firstPage = before is null;
        if (firstPage)
        {
            try { await collect.SyncAsync(player.PlayerTag, ct); }
            catch { /* API игры недоступен - покажем накопленное */ }
        }

        var now = DateTime.UtcNow;
        var access = await plus.GetAsync(tg, ct);
        var unlocked = access.Unlocked;
        var days = unlocked ? (int)CollectPlayerBattlesUseCase.Keep.TotalDays : FreeDays;
        var since = now.AddDays(-days);

        var all = await battles.GetSinceAsync(player.PlayerTag, now - CollectPlayerBattlesUseCase.Keep, ct);
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var t = BotText.For(lang);
        var visible = all.Where(b => b.BattleTimeUtc >= since).ToList();
        var lockedOlder = unlocked ? 0 : all.Count - visible.Count;

        var aggregates = firstPage ? Aggregates(all, unlocked, catalog, t) : null;

        // Фильтры. Архетип - только с Плюсом: это и есть «кому проигрываешь» по-настоящему.
        IEnumerable<PlayerBattle> rows = visible;
        if (result == "win") rows = rows.Where(b => b.Result > 0);
        else if (result == "loss") rows = rows.Where(b => b.Result < 0);
        if (mode is "ladder" or "pol" or "war")
            rows = rows.Where(b => MatchReport.ModeKey(b.Type) == mode);
        else if (mode == "other")
            rows = rows.Where(b => MatchReport.ModeKey(b.Type) is not ("ladder" or "pol" or "war"));
        if (unlocked && !string.IsNullOrEmpty(arch))
            rows = rows.Where(b => MatchReport.ArchOf(b, catalog) == arch);
        if (before is DateTime cursor) rows = rows.Where(b => b.BattleTimeUtc < cursor);

        var ordered = rows.OrderByDescending(b => b.BattleTimeUtc).ToList();
        var page = ordered.Take(PageSize).ToList();
        DateTime? next = ordered.Count > PageSize ? page[^1].BattleTimeUtc : null;

        // Заход считаем по всем боям окна, а не по отфильтрованным: «заход 4–2» - это весь вечер.
        var sessions = new List<MatchSessionDto>();
        foreach (var group in page.GroupBy(b => BattleAnalyzer.SessionAround(visible, b.BattleTimeUtc)[0].BattleTimeUtc))
        {
            var session = BattleAnalyzer.SessionAround(visible, group.Key);
            sessions.Add(new MatchSessionDto(
                session[0].BattleTimeUtc, session[^1].BattleTimeUtc,
                session.Count(b => b.Result > 0), session.Count(b => b.Result < 0), session.Count(b => b.Result == 0),
                session.Sum(b => b.TrophyChange ?? 0),
                LongestLossStreak(session) >= 3,
                new string(session.Select(b => b.Result > 0 ? 'W' : b.Result < 0 ? 'L' : 'D').ToArray()),
                group.Select(b => Row(b, all, catalog, t)).ToList()));
        }

        var state = await tracker.GetAsync(tg, ct);
        return new MatchHistoryDto(
            new TrackerStateDto(state.Available, state.Enabled, state.DmBlocked, state.MutedToday),
            unlocked, days, aggregates, sessions, lockedOlder, next);
    }

    private static int LongestLossStreak(IReadOnlyList<PlayerBattle> session)
    {
        int best = 0, run = 0;
        foreach (var b in session)
        {
            run = b.Result < 0 ? run + 1 : b.Result > 0 ? 0 : run;
            best = Math.Max(best, run);
        }
        return best;
    }

    private static MatchRowDto Row(
        PlayerBattle b, List<PlayerBattle> all, Dictionary<int, CrCatalogCard> catalog, BotText t)
    {
        var report = MatchReport.Build(b, MatchDetailCodec.Decode(b.DetailJson), all, false, catalog);
        return new MatchRowDto(
            b.Id, b.BattleTimeUtc, report.ModeKey, b.Result, b.CrownsFor, b.CrownsAgainst, b.TrophyChange,
            b.OppName, Arch(report.Arch, catalog, t), KeyCards(b.OppDeckKey, report.Arch, catalog),
            report.Verdict?.Code, b.LevelGap);
    }

    public static ArchDto? Arch(MatchReport.Arch? arch, Dictionary<int, CrCatalogCard> catalog, BotText t)
    {
        if (arch is null) return null;
        var card = Archetypes.KeyCard(arch.Key, catalog);
        return new ArchDto(arch.Key, TrackerText.ArchLabel(t, arch),
            card is int id ? GetMetaDecksUseCase.Card(id, catalog) : null);
    }

    /// <summary>Три карты, по которым колоду соперника узнают: главная архетипа, потом самые дорогие.</summary>
    private static List<MetaCardDto> KeyCards(string deckKey, MatchReport.Arch? arch, Dictionary<int, CrCatalogCard> catalog)
    {
        var keys = MetaCard.ParseDeckKey(deckKey);
        var main = arch is null ? null : Archetypes.KeyCard(arch.Key, catalog);
        return keys
            .OrderByDescending(k => Math.Abs(k) == main)
            .ThenByDescending(k => catalog.GetValueOrDefault(Math.Abs(k))?.ElixirCost ?? 0)
            .Take(3)
            .Select(k => GetMetaDecksUseCase.Card(k, catalog))
            .ToList();
    }

    /// <summary>
    /// «Кому ты проигрываешь» и уровни за месяц. Считается по всем 30 дням и без
    /// Плюса: бесплатно видна одна худшая строка, остальные - под замком.
    /// </summary>
    private static MatchAggregatesDto? Aggregates(
        List<PlayerBattle> all, bool unlocked, Dictionary<int, CrCatalogCard> catalog, BotText t)
    {
        var own = all.Where(b => b.DeckSelection is null or "collection").ToList();
        if (own.Count == 0) return null;
        var baseRate = (double)own.Count(b => b.Result > 0) / own.Count;

        var table = own
            .GroupBy(b => MatchReport.ArchOf(b, catalog))
            .Where(g => g.Key != "" && g.Count() >= ArchMinGames)
            .Select(g =>
            {
                int w = g.Count(b => b.Result > 0), l = g.Count(b => b.Result < 0), n = g.Count();
                var delta = (MatchReport.Shrunk(w, n, baseRate) - baseRate) * 100;
                var arch = new MatchReport.Arch(g.Key, Archetypes.Label(g.Key, catalog), Archetypes.IsOther(g.Key));
                return new ArchetypeRowDto(Arch(arch, catalog, t)!, n, w, l, Math.Round(delta, 1));
            })
            .OrderBy(r => r.DeltaPp)
            .ToList();
        var shown = unlocked ? table : table.Take(1).ToList();

        var withGap = own.Where(b => b.LevelGap is not null).ToList();
        var lost = withGap.Where(b => b.Result < 0).Select(b => b.LevelGap!.Value).ToList();
        var won = withGap.Where(b => b.Result > 0).Select(b => b.LevelGap!.Value).ToList();
        var under = withGap.Where(b => b.LevelGap <= -1.0).ToList();
        var even = withGap.Where(b => Math.Abs(b.LevelGap!.Value) < 0.5).ToList();
        var levels = new LevelSplitDto(
            lost.Count >= LevelAvgMinGames ? Math.Round(lost.Average(), 1) : null,
            won.Count >= LevelAvgMinGames ? Math.Round(won.Average(), 1) : null,
            under.Count >= LevelPctMinGames ? BattleAnalyzer.Percent(under.Count(b => b.Result > 0), under.Count) : null,
            even.Count >= LevelPctMinGames ? BattleAnalyzer.Percent(even.Count(b => b.Result > 0), even.Count) : null);
        var hasLevels = levels.LossAvg is not null || levels.WinAvg is not null
                        || levels.UnderWinPct is not null || levels.EvenWinPct is not null;

        return new MatchAggregatesDto(shown, table.Count - shown.Count, hasLevels ? levels : null);
    }
}

/// <summary>
/// Отчёт об одном бое: обе колоды с уровнями, башни, эликсир, соперник, вывод и
/// строки Плюса. Тексты собирает сервер - те же, что в карточке бота.
/// </summary>
public class GetMatchReportUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IPlayerAlertPrefsRepository alertPrefs,
    PlusAccess plus)
{
    public enum Outcome { Ok, NotLinked, NotFound, Locked }

    public async Task<(Outcome Outcome, MatchReportDto? Report)> ExecuteAsync(
        long tg, int id, string? lang, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return (Outcome.NotLinked, null);

        // Чужой бой по номеру не открыть: номер в ссылке - не пропуск.
        var b = await battles.GetAsync(id, ct);
        if (b is null || !string.Equals(b.PlayerTag, player.PlayerTag, StringComparison.OrdinalIgnoreCase))
            return (Outcome.NotFound, null);

        var now = DateTime.UtcNow;
        var access = await plus.GetAsync(tg, ct);
        if (!access.Unlocked && b.BattleTimeUtc < now.AddDays(-GetMatchHistoryUseCase.FreeDays))
            return (Outcome.Locked, null);

        var prefs = await alertPrefs.GetAsync(tg, ct);
        var hint = !access.Unlocked && prefs?.TrackerHintBattleUtc == b.BattleTimeUtc;
        var full = access.Unlocked || hint;

        var month = await battles.GetSinceAsync(player.PlayerTag, now - CollectPlayerBattlesUseCase.Keep, ct);
        if (!month.Any(x => x.Id == b.Id)) month = month.Append(b).OrderBy(x => x.BattleTimeUtc).ToList();
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var detail = MatchDetailCodec.Decode(b.DetailJson);
        var r = MatchReport.Build(b, detail, month, full, catalog);
        var t = BotText.For(lang);

        MatchCardDto CardDto(MatchReport.Card c)
        {
            var cat = catalog.GetValueOrDefault(c.Id);
            return new MatchCardDto(c.Id, cat?.Name ?? $"#{c.Id}",
                (c.Form > 0 ? cat?.EvoIconUrl : null) ?? cat?.IconUrl ?? "",
                c.Level, c.Form, cat?.ElixirCost ?? 0);
        }

        var myKeys = MetaCard.ParseDeckKey(b.DeckKey);
        var oppKeys = MetaCard.ParseDeckKey(b.OppDeckKey);
        var me = new MatchSideDto(
            r.Decks.Mine.Select(CardDto).ToList(), r.Decks.MyTower is { } mt ? CardDto(mt) : null,
            r.MyTowers?.KingHp, r.MyTowers?.PrincessHp?.ToList(), r.Leak?.Mine,
            detail?.Me.StartingTrophies, GetMetaDecksUseCase.CopyLink(myKeys));
        var them = new MatchSideDto(
            r.Decks.Theirs.Select(CardDto).ToList(), r.Decks.TheirTower is { } tt ? CardDto(tt) : null,
            r.OppTowers?.KingHp, r.OppTowers?.PrincessHp?.ToList(), r.Leak?.Theirs,
            detail?.Op.StartingTrophies, GetMetaDecksUseCase.CopyLink(oppKeys));

        MatchLevelsDto? levels = null;
        if (r.Levels is { } lv)
        {
            var lowest = r.Decks.Mine.FirstOrDefault(c => c.Id == lv.LowestMineId);
            levels = new MatchLevelsDto(lv.MyAvg, lv.OppAvg, lv.Gap, lowest is null ? null : CardDto(lowest));
        }

        MatchPlusDto? plusDto = null;
        if (r.Plus is { } p)
            plusDto = new MatchPlusDto(
                p.VsArch is { } va ? TrackerText.VsArch(t, r.Arch, va) : null,
                p.BetterDeck is { } bd ? TrackerText.BetterDeck(t, r.Arch, bd, catalog) : null,
                p.DeckVsArch?.Wins, p.DeckVsArch?.Losses);

        var dto = new MatchReportDto(
            b.Id, b.BattleTimeUtc, r.ModeKey, detail?.GameMode, r.Result, r.CrownsFor, r.CrownsAgainst, r.TrophyChange,
            new MatchOppDto(r.Opp.Name, r.Opp.Tag, r.Opp.Clan, r.Opp.Trophies, r.Opp.Diff, r.Opp.GlobalRank),
            GetMatchHistoryUseCase.Arch(r.Arch, catalog, t), r.OppAvgElixir, me, them, levels,
            r.Leak is { } leak ? new MatchLeakDto(leak.Mine, leak.Theirs, leak.Usual) : null,
            r.Verdict?.Code, TrackerText.Verdict(t, r.Verdict, catalog),
            new MatchReportSessionDto(r.Session.StartUtc, r.Session.Index, r.Session.Count, r.Session.Wins,
                r.Session.Losses, r.Session.Draws, r.Session.Trophies, r.Session.Strip),
            plusDto, hint && plusDto is not null, r.LockedCount, r.HasDetail, access.Unlocked);
        return (Outcome.Ok, dto);
    }
}
