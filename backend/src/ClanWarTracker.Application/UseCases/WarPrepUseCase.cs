using System.Globalization;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Военный штаб» (Клан Про): готов ли каждый соклановец к войне.
///
/// Для каждого игрока из текущего состава бот берёт его карты и уровни и собирает четыре
/// колоды без общих карт - лучшие из того, что у него есть (билдер колод, только колоды
/// топа, которые он может собрать целиком). Рядом - чем он реально играл на войне: по
/// сохранённым боям, если привязан, иначе по журналу игры. Сравнение даёт статус:
/// готов; «можно лучше» - играет колодами слабее, чем может собрать, или проигрывает
/// ими; «не готов» - не хватает карт на четыре колоды или уровни сильно ниже клана.
///
/// Уровни «чем играл» считаются по нынешней коллекции: карты не теряют уровни, и так
/// обе стороны сравнения в одной шкале.
/// </summary>
public class WarPrepUseCase(
    IClashRoyaleApi crApi,
    DeckBuilderUseCase builder,
    GetClanDisciplineUseCase discipline,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IClanRepository clans,
    IServiceSettingRepository settings,
    ISentNotificationRepository sentLog,
    INotificationSender sender,
    IPlayerAlertPrefsRepository alertPrefs,
    IMemoryCache cache)
{
    /// <summary>Рубильник владельца: «off» - пост «проверка перед КВ» в чаты не уходит.</summary>
    public const string KillSwitchKey = "channel.auto.warprep";

    public const string Ready = "ready";
    public const string Better = "better";
    public const string Weak = "weak";
    public const string None = "none";

    /// <summary>Играет колодами на столько уровней ниже, чем может собрать, - «можно лучше».</summary>
    public const double BetterLevelGap = 0.5;

    /// <summary>Лучшие колоды на столько ниже медианы клана - «не готов»: колодами тут не помочь, надо качать.</summary>
    public const double WeakLevelGap = 1.0;

    /// <summary>С меньшим числом боёв КВ процент побед ничего не говорит.</summary>
    public const int MinWarGames = 6;

    public const double LowWinPercent = 40;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(3);
    private static readonly TimeSpan PlayedWindow = TimeSpan.FromDays(14);
    private const int Parallelism = 4;
    private const int Kyiv = 180;

    public record UpgradeDto(string Name, string IconUrl, int Level);

    /// <param name="Games">Сколько боёв КВ сыграно этой колодой за две недели.</param>
    public record PlayedDto(DeckBuilderUseCase.DeckDto Deck, int Games, int Wins);

    /// <param name="Reason">no_cards, low_levels, played_lower, losing, no_war_games - для подписи; null - всё хорошо.</param>
    /// <param name="MissedDecks">Недоиграл колод за последние недели (дисциплина) - риск пропуска.</param>
    public record MemberDto(
        string Tag, string Name, string Role, bool Linked, string Status, string? Reason,
        double BestLevel, double PlayedLevel, int WarGames, int WarWins, int MissedDecks,
        List<DeckBuilderUseCase.DeckDto> Best, List<PlayedDto> Played, List<UpgradeDto> Upgrade);

    /// <summary>Строка списка у главы - без колод: их грузят по нажатию.</summary>
    public record RowDto(
        string Tag, string Name, string Role, bool Linked, string Status, string? Reason,
        double BestLevel, double PlayedLevel, int WarGames, int WarWins, int MissedDecks);

    /// <summary>Карта, которую чаще всего надо качать в клане.</summary>
    public record WeakCardDto(string Name, string IconUrl, int Players);

    public record SummaryDto(
        int Total, int Ready, int Better, int Weak, int Unknown, double ClanBestLevel,
        List<WeakCardDto> WeakCards, List<RowDto> Members, DateTime BuiltUtc);

    private sealed record Full(SummaryDto Summary, Dictionary<string, MemberDto> Members);

    /// <summary>Статус по цифрам. Отдельно от загрузки - чтобы правила проверялись тестами.</summary>
    public static (string Status, string? Reason) Classify(
        bool complete, double best, double played, int games, int wins, bool playsBest, double clanMedian)
    {
        if (!complete) return (Weak, "no_cards");
        if (clanMedian > 0 && best < clanMedian - WeakLevelGap) return (Weak, "low_levels");
        if (played > 0 && played <= best - BetterLevelGap) return (Better, "played_lower");
        // Проигрывает, но не теми колодами, что подобрал бот, - им стоит попробовать
        if (!playsBest && games >= MinWarGames && 100.0 * wins / games < LowWinPercent) return (Better, "losing");
        return (Ready, games == 0 ? "no_war_games" : null);
    }

    public static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        if (v.Count == 0) return 0;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    /// <summary>Сводка по клану для главы. null - меты ещё нет (билдеру не из чего собирать).</summary>
    public async Task<SummaryDto?> SummaryAsync(Clan clan, bool fresh, CancellationToken ct = default) =>
        (await FullAsync(clan, fresh, ct))?.Summary;

    /// <summary>Один игрок со всеми колодами. null - нет такого в составе или меты ещё нет.</summary>
    public async Task<MemberDto?> MemberAsync(Clan clan, string tag, CancellationToken ct = default)
    {
        var full = await FullAsync(clan, false, ct);
        return full?.Members.GetValueOrDefault(tag.ToUpperInvariant());
    }

    /// <summary>
    /// Одна проверка клана за раз: в среду карточку откроют десятки человек почти разом, и
    /// без замка каждый запустил бы свой обход всего состава по API игры.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> Gates = new();

    private async Task<Full?> FullAsync(Clan clan, bool fresh, CancellationToken ct)
    {
        var key = $"warprep:{clan.Id}";
        bool Usable(Full? f) => f is not null && (!fresh || DateTime.UtcNow - f.Summary.BuiltUtc < TimeSpan.FromMinutes(10));
        if (cache.TryGetValue(key, out Full? hit) && Usable(hit)) return hit;

        var gate = Gates.GetOrAdd(clan.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Пока ждали, проверку мог закончить соседний запрос
            if (cache.TryGetValue(key, out hit) && Usable(hit)) return hit;
            var full = await BuildAsync(clan, ct);
            if (full is not null)
                cache.Set(key, full, new MemoryCacheEntryOptions { Size = 5, AbsoluteExpirationRelativeToNow = CacheTtl });
            return full;
        }
        finally { gate.Release(); }
    }

    private async Task<Full?> BuildAsync(Clan clan, CancellationToken ct)
    {
        var planner = await builder.WarPlannerAsync(ct);
        var reader = await builder.DeckReaderAsync(ct);
        if (planner is null || reader is null) return null;

        var roster = await crApi.GetClanRosterAsync(clan.ClanTag, ct);
        var linked = (await players.GetByClanIdAsync(clan.Id, ct))
            .Where(p => p.TelegramUserId is not null)
            .Select(p => p.PlayerTag.ToUpperInvariant())
            .ToHashSet();

        var missed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var p in (await discipline.ExecuteAsync(clan, ct)).Skippers)
                missed[p.PlayerTag] = p.MissedDecks;
        }
        catch (Exception) when (!ct.IsCancellationRequested) { /* без дисциплины - без флага риска */ }

        // Бои КВ привязанных - одним запросом из базы: контекст базы не терпит параллельных чтений
        var now = DateTime.UtcNow;
        var saved = linked.Count == 0
            ? new Dictionary<string, List<(string DeckKey, bool Won)>>()
            : (await battles.GetForTagsBetweenAsync(linked.ToList(), now - PlayedWindow, now, ct))
                .Where(b => MatchReport.ModeKey(b.Type) == "war")
                .GroupBy(b => b.PlayerTag.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Select(b => (b.DeckKey, Won: b.Result > 0)).ToList());

        using var gate = new SemaphoreSlim(Parallelism);
        var raw = await Task.WhenAll(roster.Select(async m =>
        {
            await gate.WaitAsync(ct);
            try { return await AssessAsync(m, linked.Contains(m.Tag.ToUpperInvariant()), saved, planner, reader, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { return Empty(m, linked.Contains(m.Tag.ToUpperInvariant())); }
            finally { gate.Release(); }
        }));

        // Медиана клана - по тем, у кого вообще собрались четыре колоды
        var median = Median(raw.Where(r => r.Best.Count == 4).Select(r => r.BestLevel));
        var members = new Dictionary<string, MemberDto>();
        foreach (var r in raw)
        {
            var playsBest = r.Played.Count > 0
                && r.Played.Where(p => r.Best.Any(b => b.Key == p.Deck.Key)).Sum(p => p.Games) * 2 >= r.Played.Sum(p => p.Games);
            var (status, reason) = r.Status == None
                ? (None, "no_profile")
                : Classify(r.Best.Count == 4, r.BestLevel, r.PlayedLevel, r.WarGames, r.WarWins, playsBest, median);
            members[r.Tag.ToUpperInvariant()] = r with
            {
                Status = status, Reason = reason,
                MissedDecks = missed.GetValueOrDefault(r.Tag),
            };
        }

        var list = members.Values
            .OrderBy(m => m.Status switch { Weak => 0, Better => 1, Ready => 2, _ => 3 })
            .ThenBy(m => m.BestLevel)
            .ToList();
        var weakCards = list
            .Where(m => m.Status is Weak or Better)
            .SelectMany(m => m.Upgrade.Select(u => (u.Name, u.IconUrl)))
            .GroupBy(u => u.Name)
            .Select(g => new WeakCardDto(g.Key, g.First().IconUrl, g.Count()))
            .Where(w => w.Players >= 2)
            .OrderByDescending(w => w.Players)
            .Take(5)
            .ToList();

        var summary = new SummaryDto(
            list.Count, list.Count(m => m.Status == Ready), list.Count(m => m.Status == Better),
            list.Count(m => m.Status == Weak), list.Count(m => m.Status == None), Math.Round(median, 1), weakCards,
            list.Select(m => new RowDto(m.Tag, m.Name, m.Role, m.Linked, m.Status, m.Reason,
                m.BestLevel, m.PlayedLevel, m.WarGames, m.WarWins, m.MissedDecks)).ToList(),
            DateTime.UtcNow);
        return new Full(summary, members);
    }

    private static MemberDto Empty(CrClanMember m, bool linked) =>
        new(m.Tag, m.Name, m.Role, linked, None, "no_profile", 0, 0, 0, 0, 0, [], [], []);

    private async Task<MemberDto> AssessAsync(
        CrClanMember m, bool linked, IReadOnlyDictionary<string, List<(string DeckKey, bool Won)>> saved,
        Func<Dictionary<string, CrCard>, DeckBuilderUseCase.WarResult> planner,
        Func<string, Dictionary<string, CrCard>, DeckBuilderUseCase.DeckDto?> reader, CancellationToken ct)
    {
        var info = await crApi.GetPlayerInfoAsync(m.Tag, ct);
        if (info is null || info.Cards.Count == 0) return Empty(m, linked);
        var mine = info.Cards
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var war = planner(mine);
        var best = war.Decks;
        var bestLevel = best.Count == 0 ? 0 : Math.Round(best.Average(d => d.AvgLevel), 1);

        // Чем играл на войне: свои сохранённые бои у привязанных, журнал игры - у остальных
        List<(string DeckKey, bool Won)> games;
        if (saved.TryGetValue(m.Tag.ToUpperInvariant(), out var own) && own.Count > 0) games = own;
        else
        {
            var since = DateTime.UtcNow - PlayedWindow;
            games = (await crApi.GetRecentBattlesAsync(m.Tag, ct))
                .Where(b => MatchReport.ModeKey(b.Type) == "war" && b.MyDeck.Count == 8 && b.BattleTimeUtc >= since)
                .Select(b => (MetaCard.DeckKey(b.MyDeck.Select(MetaCard.Key)), b.Won))
                .ToList();
        }

        var played = games
            .GroupBy(g => g.DeckKey)
            .OrderByDescending(g => g.Count())
            .Take(4)
            .Select(g => reader(g.Key, mine) is { } d ? new PlayedDto(d, g.Count(), g.Count(x => x.Won)) : null)
            .OfType<PlayedDto>()
            .ToList();
        var playedGames = played.Sum(p => p.Games);
        var playedLevel = playedGames == 0 ? 0 : Math.Round(played.Sum(p => p.Deck.AvgLevel * p.Games) / playedGames, 1);

        // Что качать: самые отстающие карты в его же лучших колодах
        var upgrade = best
            .SelectMany(d => d.Cards)
            .Where(c => c.Owned && c.Level < bestLevel - 0.5)
            .GroupBy(c => c.Name)
            .Select(g => g.First())
            .OrderBy(c => c.Level)
            .Take(3)
            .Select(c => new UpgradeDto(c.Name, c.BaseIconUrl, c.Level))
            .ToList();

        return new MemberDto(m.Tag, m.Name, m.Role, linked, Ready, null, bestLevel, playedLevel,
            games.Count, games.Count(g => g.Won), 0, best, played, upgrade);
    }

    /* ---------------- Рассылка колод и пост перед КВ ---------------- */

    private const string DmKind = "warprepdm";
    private const string PostKind = "warpreppost";

    /// <summary>Неделя по Киеву - ключ «уже отправляли на этой неделе».</summary>
    public static string WeekKey(DateTime utc) =>
        ISOWeek.GetYear(utc.AddMinutes(Kyiv)) + "w" + ISOWeek.GetWeekOfYear(utc.AddMinutes(Kyiv));

    public record SendResult(int Sent, int Skipped);

    /// <summary>
    /// Глава нажал «Разослать колоды»: каждому привязанному соклановцу - его четыре колоды в
    /// личку со ссылками «скопировать в игру». Раз в неделю на человека: вторая кнопка в тот
    /// же день не завалит людей одинаковыми сообщениями.
    /// </summary>
    public async Task<SendResult> SendAsync(Clan clan, CancellationToken ct = default)
    {
        var full = await FullAsync(clan, false, ct);
        if (full is null) return new SendResult(0, 0);
        var week = WeekKey(DateTime.UtcNow);
        var lang = NotificationSettings.Parse(clan.NotificationSettingsJson).Language;
        var people = (await players.GetByClanIdAsync(clan.Id, ct))
            .Where(p => p.TelegramUserId is not null)
            .GroupBy(p => p.TelegramUserId!.Value)
            .Select(g => g.First())
            .ToList();

        int sent = 0, skipped = 0;
        foreach (var p in people)
        {
            var m = full.Members.GetValueOrDefault(p.PlayerTag.ToUpperInvariant());
            var tg = p.TelegramUserId!.Value;
            if (m is null || m.Best.Count == 0) { skipped++; continue; }
            var prefs = await alertPrefs.GetAsync(tg, ct);
            if (prefs?.DmBlocked == true) { skipped++; continue; }
            if (!await sentLog.TryClaimAsync(DmKind, $"{clan.Id}:{tg}:{week}", ct)) { skipped++; continue; }

            var t = WarPrepText.For(prefs?.Lang ?? lang);
            var (text, rows) = Dm(t, clan.Name, m);
            var r = await sender.SendDmAsync(tg, text, rows, false, ct);
            if (r.Delivered) sent++;
            else
            {
                skipped++;
                if (r.Blocked)
                {
                    var tracked = await alertPrefs.GetOrCreateAsync(tg, ct);
                    tracked.DmBlocked = true;
                    await alertPrefs.SaveChangesAsync(ct);
                }
                else await sentLog.ReleaseAsync(DmKind, $"{clan.Id}:{tg}:{week}", ct);
            }
            await Task.Delay(40, ct);
        }
        return new SendResult(sent, skipped);
    }

    internal static (string Text, IReadOnlyList<IReadOnlyList<BotButton>> Rows) Dm(WarPrepText t, string clanName, MemberDto m)
    {
        var lines = new List<string> { string.Format(t.DmHead, clanName) };
        lines.Add(m.Status switch
        {
            Better when m.Reason == "played_lower" => string.Format(t.DmBetterLevel,
                m.PlayedLevel.ToString("0.0", CultureInfo.InvariantCulture), m.BestLevel.ToString("0.0", CultureInfo.InvariantCulture)),
            Better => t.DmBetterLosing,
            Weak => t.DmWeak,
            _ => t.DmReady,
        });
        lines.Add("");
        for (var i = 0; i < m.Best.Count; i++)
            lines.Add($"{i + 1}) " + string.Join(", ", m.Best[i].Cards.Select(c => c.Name))
                      + $" — {m.Best[i].AvgLevel.ToString("0.0", CultureInfo.InvariantCulture)}");
        if (m.Upgrade.Count > 0)
        {
            lines.Add("");
            lines.Add(string.Format(t.DmUpgrade, string.Join(", ", m.Upgrade.Select(u => u.Name))));
        }

        var rows = new List<IReadOnlyList<BotButton>>();
        var deckButtons = m.Best
            .Select((d, i) => d.CopyLink is null ? null : new BotButton(string.Format(t.DeckButton, i + 1), Url: d.CopyLink))
            .OfType<BotButton>()
            .ToList();
        for (var i = 0; i < deckButtons.Count; i += 2) rows.Add(deckButtons.Skip(i).Take(2).ToList());
        rows.Add([new BotButton(t.OpenButton, Url: "startapp:warprep")]);
        return (string.Join("\n", lines), rows);
    }

    /// <summary>
    /// Фоновый проход: в среду вечером (17-21 по Киеву), накануне войны, кланам с Клан Про
    /// и подключённым чатом - пост «проверка перед КВ»: сколько готовы, сколько могут
    /// собрать колоды сильнее, чего не хватает клану. Один раз в неделю на клан.
    /// </summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var local = now.AddMinutes(Kyiv);
        if (local.DayOfWeek != DayOfWeek.Wednesday || local.Hour is < 17 or >= 21) return 0;
        if (string.Equals(await settings.GetAsync(KillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase)) return 0;

        var pro = await players.SponsoredClanIdsAsync(now, ct);
        if (pro.Count == 0) return 0;
        var week = WeekKey(now);
        var posted = 0;
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (!pro.Contains(clan.Id) || clan.TelegramChatId == 0) continue;
            if (!await sentLog.TryClaimAsync(PostKind, $"{clan.Id}:{week}", ct)) continue;
            try
            {
                var s = await SummaryAsync(clan, fresh: true, ct);
                if (s is null || s.Total == 0) { await sentLog.ReleaseAsync(PostKind, $"{clan.Id}:{week}", ct); continue; }
                var t = WarPrepText.For(NotificationSettings.Parse(clan.NotificationSettingsJson).Language);
                var ok = await sender.SendToChatWithButtonsAsync(clan.TelegramChatId, Post(t, clan.Name, s),
                    [[new BotButton(t.OpenButton, Url: "startapp:warprep")]], clan.TelegramMessageThreadId, ct);
                if (ok) posted++;
                else await sentLog.ReleaseAsync(PostKind, $"{clan.Id}:{week}", ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await sentLog.ReleaseAsync(PostKind, $"{clan.Id}:{week}", ct);
            }
        }
        return posted;
    }

    internal static string Post(WarPrepText t, string clanName, SummaryDto s)
    {
        var known = s.Total - s.Unknown;
        var lines = new List<string>
        {
            string.Format(t.PostHead, clanName),
            "",
            string.Format(t.PostReady, s.Ready, known),
        };
        if (s.Better > 0) lines.Add(string.Format(t.PostBetter, s.Better));
        if (s.Weak > 0) lines.Add(string.Format(t.PostWeak, s.Weak));
        if (s.WeakCards.Count > 0)
        {
            lines.Add("");
            lines.Add(string.Format(t.PostWeakCards, string.Join(", ", s.WeakCards.Select(w => w.Name))));
        }
        lines.Add("");
        lines.Add(t.PostTail);
        return string.Join("\n", lines);
    }
}
