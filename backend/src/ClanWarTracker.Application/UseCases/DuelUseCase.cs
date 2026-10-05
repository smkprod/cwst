using System.Globalization;
using System.Text.RegularExpressions;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Лига дуэлей 1х1.
///
/// Вызов живёт в кнопке сообщения (в чате клана или инлайн-карточкой в любом чате):
/// данные вызова зашиты в callback, и в базу ничего не пишется, пока соперник не
/// нажал «Принять». Дальше бот сам смотрит журнал боёв: бой, где по разные стороны
/// стоят ровно эти два тега и который сыгран после принятия, засчитывается. Чужой
/// бой так не засчитать, а скриншоты и споры не нужны вовсе.
/// </summary>
public partial class DuelUseCase(
    IDuelRepository duels,
    IPlayerRepository players,
    IClashRoyaleApi crApi,
    INotificationSender sender)
{
    public const int StartRating = 1000;

    /// <summary>Сколько рейтинговых дуэлей у одной пары за сутки. Дальше - товарищеские: против фарма с другом.</summary>
    public const int RatedPerPairPerDay = 3;

    /// <summary>Сколько живёт непринятый вызов.</summary>
    public static readonly TimeSpan ChallengeTtl = TimeSpan.FromHours(24);

    /// <summary>Сколько даётся на всю дуэль: добавиться в друзья и сыграть.</summary>
    public static TimeSpan Timeout(int bestOf) => TimeSpan.FromMinutes(bestOf >= 3 ? 90 : 45);

    /// <summary>Как часто перечитываем журнал идущей дуэли. Кэш API - 20 секунд, чаще бессмысленно.</summary>
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(25);

    /* ---------------- Лиги ---------------- */

    /// <summary>Нижние границы лиг: Бронза, Серебро, Золото, Алмаз, Мастер, Легенда. Старт 1000 - Серебро.</summary>
    public static readonly int[] LeagueFloors = [0, 1000, 1150, 1300, 1450, 1600];
    public static readonly string[] LeagueKeys = ["bronze", "silver", "gold", "diamond", "master", "legend"];
    private static readonly string[] LeagueIcons = ["🥉", "🥈", "🥇", "💎", "👑", "🔥"];

    public static int LeagueIndex(int rating)
    {
        var i = 0;
        while (i + 1 < LeagueFloors.Length && rating >= LeagueFloors[i + 1]) i++;
        return i;
    }

    public static string LeagueLabel(int rating, DuelText t)
    {
        var i = LeagueIndex(rating);
        return $"{LeagueIcons[i]} {t.League(i)}";
    }

    /* ---------------- Эло ---------------- */

    /// <summary>
    /// Изменение рейтинга победителя (проигравший теряет столько же). Новичкам первые
    /// десять дуэлей коэффициент выше - быстрее находят своё место. Bo3 весит больше:
    /// серия из трёх боёв говорит о силе больше, чем один бой.
    /// </summary>
    public static int EloDelta(int winnerRating, int loserRating, int winnerGames, int loserGames, int bestOf)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10, (loserRating - winnerRating) / 400.0));
        var k = (winnerGames < 10 || loserGames < 10) ? 40.0 : 24.0;
        if (bestOf >= 3) k *= 1.25;
        return Math.Max(1, (int)Math.Round(k * (1 - expected)));
    }

    /* ---------------- Ссылка в друзья ---------------- */

    [GeneratedRegex(@"https?://link\.clashroyale\.com/\S+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"[?&]tag=([0-9A-Za-z]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TagParam();

    /// <summary>Есть ли в тексте ссылка из игры - чтобы бот понял, что прислали именно её.</summary>
    public static bool LooksLikeFriendLink(string? text) =>
        text is not null && text.Contains("link.clashroyale.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Достаёт ссылку «добавить в друзья» и тег из неё. Тег нужен, чтобы человек не
    /// выдал чужую ссылку за свою: соперник добавил бы в друзья не того.
    /// </summary>
    public static bool TryParseFriendLink(string text, out string link, out string tag)
    {
        link = "";
        tag = "";
        var m = LinkRegex().Match(text);
        if (!m.Success) return false;
        var url = m.Value.TrimEnd('.', ',', ')', '»', '"', '\'');
        if (!url.Contains("invite/friend", StringComparison.OrdinalIgnoreCase)) return false;
        var t = TagParam().Match(url);
        if (!t.Success) return false;
        if (url.Length > 400) return false;
        link = url;
        tag = LinkPlayerUseCase.Normalize(t.Groups[1].Value);
        return true;
    }

    public enum LinkOutcome { Joined, Updated, NotLinked, BadLink, WrongTag }

    public record LinkResult(LinkOutcome Outcome, DuelProfile? Profile, string? LinkTag = null, string? PlayerTag = null);

    /// <summary>Вступление в лигу или новая ссылка в друзья.</summary>
    public async Task<LinkResult> SetFriendLinkAsync(long telegramUserId, string? lang, string text, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return new LinkResult(LinkOutcome.NotLinked, null);
        if (!TryParseFriendLink(text, out var link, out var tag)) return new LinkResult(LinkOutcome.BadLink, null);

        var own = LinkPlayerUseCase.Normalize(player.PlayerTag);
        if (!string.Equals(tag, own, StringComparison.OrdinalIgnoreCase))
            return new LinkResult(LinkOutcome.WrongTag, null, tag, own);

        var profile = await duels.GetProfileAsync(telegramUserId, ct);
        if (profile is null)
        {
            profile = new DuelProfile
            {
                TelegramUserId = telegramUserId,
                PlayerTag = own,
                Name = Cut(player.Name, 64),
                FriendLink = link,
                Rating = StartRating,
                Peak = StartRating,
                Lang = Cut(lang, 8),
                JoinedUtc = DateTime.UtcNow,
            };
            await duels.AddProfileAsync(profile, ct);
            return new LinkResult(LinkOutcome.Joined, profile);
        }

        profile.FriendLink = link;
        profile.PlayerTag = own;
        profile.Name = Cut(player.Name, 64);
        if (lang is not null) profile.Lang = Cut(lang, 8);
        await duels.SaveChangesAsync(ct);
        return new LinkResult(LinkOutcome.Updated, profile);
    }

    public Task<DuelProfile?> GetProfileAsync(long telegramUserId, CancellationToken ct = default) =>
        duels.GetProfileAsync(telegramUserId, ct);

    public Task<int> RankOfAsync(DuelProfile p, CancellationToken ct = default) =>
        p.Games == 0 ? duels.CountProfilesAsync(ct) : duels.RankOfAsync(p.Rating, ct);

    /* ---------------- Вызов ---------------- */

    /// <summary>
    /// Callback кнопки «Принять»: кто вызвал, Bo, кого (0 - любой), когда (минуты Unix в base36).
    /// Укладывается в 64 байта лимита Telegram с запасом.
    /// </summary>
    public static string AcceptData(long challenger, int bestOf, long target, DateTime nowUtc) =>
        $"dl|a|{challenger}|{bestOf}|{target}|{ToBase36(new DateTimeOffset(nowUtc).ToUnixTimeSeconds() / 60)}";

    public record AcceptRequest(long Challenger, int BestOf, long Target, DateTime CreatedUtc);

    public static AcceptRequest? ParseAccept(string data)
    {
        var p = data.Split('|');
        if (p.Length != 6 || p[0] != "dl" || p[1] != "a") return null;
        if (!long.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out var challenger)) return null;
        if (!int.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out var bo) || (bo != 1 && bo != 3)) return null;
        if (!long.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out var target)) return null;
        var minutes = FromBase36(p[5]);
        if (minutes is null) return null;
        return new AcceptRequest(challenger, bo, target,
            DateTimeOffset.FromUnixTimeSeconds(minutes.Value * 60).UtcDateTime);
    }

    /// <summary>Текст и кнопки открытого вызова.</summary>
    public (string Text, List<IReadOnlyList<BotButton>> Rows) OpenCard(
        DuelProfile challenger, int bestOf, long target, string? targetName, DateTime nowUtc)
    {
        var t = DuelText.For(challenger.Lang);
        var bo = $"Bo{bestOf}";
        var text = target != 0 && targetName is not null
            ? string.Format(t.OpenTargeted, bo, challenger.Name, LeagueLabel(challenger.Rating, t), challenger.Rating, targetName)
            : string.Format(t.Open, bo, challenger.Name, LeagueLabel(challenger.Rating, t), challenger.Rating);
        var rows = new List<IReadOnlyList<BotButton>>
        {
            new[] { new BotButton(t.BtnAccept, CallbackData: AcceptData(challenger.TelegramUserId, bestOf, target, nowUtc)) },
            new[] { new BotButton(t.BtnLeague, Url: "startapp:duel") },
        };
        return (text, rows);
    }

    public enum AcceptOutcome { Started, Self, NotTarget, NeedJoin, ChallengerGone, YouBusy, ThemBusy, Taken, Stale }

    public record AcceptResult(AcceptOutcome Outcome, Duel? Duel = null);

    /// <summary>Где лежит карточка, которую принимают: сообщение в чате или инлайн-сообщение.</summary>
    public record CardPlace(long? ChatId, int? MessageId, string? InlineMessageId);

    public async Task<AcceptResult> AcceptAsync(AcceptRequest req, long acceptor, string? acceptorLang, CardPlace place,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (now - req.CreatedUtc > ChallengeTtl) return new AcceptResult(AcceptOutcome.Stale);
        if (acceptor == req.Challenger) return new AcceptResult(AcceptOutcome.Self);
        if (req.Target != 0 && acceptor != req.Target) return new AcceptResult(AcceptOutcome.NotTarget);

        var b = await duels.GetProfileAsync(acceptor, ct);
        if (b is null) return new AcceptResult(AcceptOutcome.NeedJoin);
        var a = await duels.GetProfileAsync(req.Challenger, ct);
        if (a is null) return new AcceptResult(AcceptOutcome.ChallengerGone);
        if (string.Equals(a.PlayerTag, b.PlayerTag, StringComparison.OrdinalIgnoreCase)) return new AcceptResult(AcceptOutcome.Self);

        if (await duels.GetActiveForUserAsync(acceptor, ct) is not null) return new AcceptResult(AcceptOutcome.YouBusy);
        if (await duels.GetActiveForUserAsync(req.Challenger, ct) is not null) return new AcceptResult(AcceptOutcome.ThemBusy);

        if (acceptorLang is not null && b.Lang is null) b.Lang = Cut(acceptorLang, 8);

        var rated = await duels.CountRatedBetweenAsync(a.TelegramUserId, b.TelegramUserId, now.AddDays(-1), ct)
                    < RatedPerPairPerDay;
        var duel = new Duel
        {
            BestOf = req.BestOf,
            State = DuelState.Active,
            ATelegramUserId = a.TelegramUserId,
            ATag = a.PlayerTag,
            AName = a.Name,
            BTelegramUserId = b.TelegramUserId,
            BTag = b.PlayerTag,
            BName = b.Name,
            RatingA = a.Rating,
            RatingB = b.Rating,
            Rated = rated,
            AcceptedUtc = now,
            ChatId = place.ChatId,
            MessageId = place.MessageId,
            InlineMessageId = place.InlineMessageId,
            Lang = a.Lang,
        };
        if (!await duels.TryAddDuelAsync(duel, ct)) return new AcceptResult(AcceptOutcome.Taken);

        await RenderAsync(duel, a, b, ct);

        // Обоим в личку - ссылка соперника в друзья. Не дошло (бот не запущен) -
        // не беда: та же кнопка висит под карточкой в чате.
        await DmStartAsync(a, b, duel, ct);
        await DmStartAsync(b, a, duel, ct);
        return new AcceptResult(AcceptOutcome.Started, duel);
    }

    private async Task DmStartAsync(DuelProfile to, DuelProfile opp, Duel duel, CancellationToken ct)
    {
        var t = DuelText.For(to.Lang);
        try
        {
            await sender.SendDmAsync(to.TelegramUserId, string.Format(t.DmStarted, opp.Name, $"Bo{duel.BestOf}"),
                [[new BotButton(string.Format(t.BtnAddFriend, opp.Name), Url: opp.FriendLink)]], ct: ct);
        }
        catch { /* личка - приятное дополнение, карточка в чате важнее */ }
    }

    public enum CancelOutcome { Cancelled, NotYours, CantCancel, Gone }

    public async Task<CancelOutcome> CancelAsync(int duelId, long telegramUserId, CancellationToken ct = default)
    {
        var duel = await duels.GetDuelAsync(duelId, ct);
        if (duel is null || duel.State != DuelState.Active) return CancelOutcome.Gone;
        if (duel.ATelegramUserId != telegramUserId && duel.BTelegramUserId != telegramUserId) return CancelOutcome.NotYours;
        if (duel.ScoreA + duel.ScoreB > 0) return CancelOutcome.CantCancel;

        duel.State = DuelState.Cancelled;
        duel.FinishedUtc = DateTime.UtcNow;
        await duels.SaveChangesAsync(ct);
        await RenderAsync(duel, null, null, ct);
        return CancelOutcome.Cancelled;
    }

    /// <summary>«Проверить счёт» - внеочередной проход по одной дуэли.</summary>
    public async Task<bool> RefreshAsync(int duelId, long telegramUserId, CancellationToken ct = default)
    {
        var duel = await duels.GetDuelAsync(duelId, ct);
        if (duel is null || duel.State != DuelState.Active) return false;
        if (duel.ATelegramUserId != telegramUserId && duel.BTelegramUserId != telegramUserId) return false;
        await CheckAsync(duel, DateTime.UtcNow, force: true, ct);
        return true;
    }

    /* ---------------- Автосчёт ---------------- */

    /// <summary>Проход по идущим дуэлям: счёт из журнала, итог, просрочка. Возвращает, сколько закрыто.</summary>
    public async Task<int> PollAsync(CancellationToken ct = default)
    {
        var active = await duels.GetActiveAsync(ct);
        var closed = 0;
        var now = DateTime.UtcNow;
        foreach (var duel in active)
        {
            try
            {
                if (await CheckAsync(duel, now, force: false, ct)) closed++;
            }
            catch
            {
                // Один недоступный журнал не должен держать остальные дуэли.
            }
        }
        return closed;
    }

    /// <returns>true - дуэль закрыта (итог или просрочка).</returns>
    private async Task<bool> CheckAsync(Duel duel, DateTime now, bool force, CancellationToken ct)
    {
        if (!force && duel.CheckedUtc is { } last && now - last < PollEvery) return false;
        duel.CheckedUtc = now;

        var battles = await crApi.GetBattlesForAutoResultAsync(duel.ATag, ct);
        var facts = battles.Select(b => new TournamentAutoResult.BattleFact(
            b.BattleTimeUtc,
            b.TeamTags.Select(LinkPlayerUseCase.Normalize).ToList(),
            b.OpponentTags.Select(LinkPlayerUseCase.Normalize).ToList(),
            b.CrownsFor,
            b.CrownsAgainst));

        var winsNeeded = duel.BestOf >= 3 ? 2 : 1;
        // Минута запаса: часы API и наши расходятся, а бой, начатый до нажатия «Принять»,
        // всё равно закончится позже него.
        var outcome = TournamentAutoResult.Resolve(facts,
            [LinkPlayerUseCase.Normalize(duel.ATag)], [LinkPlayerUseCase.Normalize(duel.BTag)],
            winsNeeded, duel.AcceptedUtc.AddMinutes(-1));

        var moved = outcome.ScoreA != duel.ScoreA || outcome.ScoreB != duel.ScoreB;
        duel.ScoreA = outcome.ScoreA;
        duel.ScoreB = outcome.ScoreB;

        if (outcome.Decided)
        {
            await FinishAsync(duel, ct);
            return true;
        }

        if (now - duel.AcceptedUtc > Timeout(duel.BestOf))
        {
            duel.State = DuelState.Expired;
            duel.FinishedUtc = now;
            await duels.SaveChangesAsync(ct);
            await RenderAsync(duel, null, null, ct);
            return true;
        }

        await duels.SaveChangesAsync(ct);
        if (moved) await RenderAsync(duel, null, null, ct);
        return false;
    }

    private async Task FinishAsync(Duel duel, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var profiles = await duels.GetProfilesAsync([duel.ATelegramUserId, duel.BTelegramUserId], ct);
        var a = profiles.FirstOrDefault(p => p.TelegramUserId == duel.ATelegramUserId);
        var b = profiles.FirstOrDefault(p => p.TelegramUserId == duel.BTelegramUserId);

        var aWon = duel.ScoreA > duel.ScoreB;
        duel.State = DuelState.Finished;
        duel.FinishedUtc = now;

        if (a is not null && b is not null)
        {
            // Рейтинг считаем от текущего, а не от снимка при принятии: между ними
            // мог закончиться чужой матч, и его результат не должен пропасть.
            var (w, l) = aWon ? (a, b) : (b, a);
            var delta = duel.Rated ? EloDelta(w.Rating, l.Rating, w.Games, l.Games, duel.BestOf) : 0;
            duel.RatingA = a.Rating;
            duel.RatingB = b.Rating;
            duel.DeltaA = aWon ? delta : -delta;
            duel.DeltaB = aWon ? -delta : delta;

            w.Rating += delta;
            l.Rating = Math.Max(0, l.Rating - delta);
            w.Peak = Math.Max(w.Peak, w.Rating);
            w.Wins++;
            l.Losses++;
            w.Games++;
            l.Games++;
            w.LastDuelUtc = now;
            l.LastDuelUtc = now;
        }

        await duels.SaveChangesAsync(ct);
        await RenderAsync(duel, a, b, ct);

        if (a is not null && b is not null)
        {
            await DmResultAsync(a, b, duel, duel.RatingA, duel.DeltaA, aWon, ct);
            await DmResultAsync(b, a, duel, duel.RatingB, duel.DeltaB, !aWon, ct);
        }
    }

    private async Task DmResultAsync(DuelProfile to, DuelProfile opp, Duel duel, int before, int delta, bool won,
        CancellationToken ct)
    {
        var t = DuelText.For(to.Lang);
        var mine = to.TelegramUserId == duel.ATelegramUserId ? duel.ScoreA : duel.ScoreB;
        var theirs = to.TelegramUserId == duel.ATelegramUserId ? duel.ScoreB : duel.ScoreA;
        var text = string.Format(won ? t.DmWon : t.DmLost, opp.Name, $"{mine}:{theirs}",
            before, to.Rating, Signed(delta), LeagueLabel(to.Rating, t));
        try
        {
            await sender.SendDmAsync(to.TelegramUserId, text,
                [[new BotButton(t.BtnChallenge, SwitchInline: "duel"), new BotButton(t.BtnLeague, Url: "startapp:duel")]],
                silent: true, ct: ct);
        }
        catch { /* итог и так виден в чате */ }
    }

    /* ---------------- Карточка ---------------- */

    /// <summary>Перерисовывает карточку дуэли там, где её приняли.</summary>
    private async Task RenderAsync(Duel duel, DuelProfile? a, DuelProfile? b, CancellationToken ct)
    {
        if (duel.State == DuelState.Active && (a is null || b is null))
        {
            var ps = await duels.GetProfilesAsync([duel.ATelegramUserId, duel.BTelegramUserId], ct);
            a ??= ps.FirstOrDefault(p => p.TelegramUserId == duel.ATelegramUserId);
            b ??= ps.FirstOrDefault(p => p.TelegramUserId == duel.BTelegramUserId);
        }

        var (text, rows) = Card(duel, a, b);
        try
        {
            if (duel.InlineMessageId is { } inlineId)
                await sender.EditInlineMessageAsync(inlineId, text, rows, ct);
            else if (duel.ChatId is { } chat && duel.MessageId is { } msg)
                await sender.EditUserMessageAsync(chat, msg, text, rows, ct);
        }
        catch { /* карточку удалили - дуэль от этого не ломается */ }
    }

    public static (string Text, List<IReadOnlyList<BotButton>> Rows) Card(Duel duel, DuelProfile? a, DuelProfile? b)
    {
        var t = DuelText.For(duel.Lang);
        var bo = $"Bo{duel.BestOf}";
        var rows = new List<IReadOnlyList<BotButton>>();

        switch (duel.State)
        {
            case DuelState.Active:
            {
                var minutes = (int)Timeout(duel.BestOf).TotalMinutes;
                var text = string.Format(t.Active, bo, duel.AName, duel.RatingA, duel.BName, duel.RatingB,
                    duel.ScoreA, duel.ScoreB, minutes);
                if (!duel.Rated) text += t.Unrated;

                var friends = new List<BotButton>();
                if (a is not null) friends.Add(new BotButton(string.Format(t.BtnAddFriend, Short(duel.AName)), Url: a.FriendLink));
                if (b is not null) friends.Add(new BotButton(string.Format(t.BtnAddFriend, Short(duel.BName)), Url: b.FriendLink));
                if (friends.Count > 0) rows.Add(friends);

                var actions = new List<BotButton> { new(t.BtnRefresh, CallbackData: $"dl|r|{duel.Id}") };
                if (duel.ScoreA + duel.ScoreB == 0) actions.Add(new BotButton(t.BtnCancel, CallbackData: $"dl|x|{duel.Id}"));
                rows.Add(actions);
                return (text, rows);
            }
            case DuelState.Finished:
            {
                var aWon = duel.ScoreA > duel.ScoreB;
                var (wName, lName) = aWon ? (duel.AName, duel.BName) : (duel.BName, duel.AName);
                var score = aWon ? $"{duel.ScoreA}:{duel.ScoreB}" : $"{duel.ScoreB}:{duel.ScoreA}";
                string text;
                if (duel.Rated)
                {
                    var rowA = string.Format(t.RatingRow, duel.AName, duel.RatingA, duel.RatingA + duel.DeltaA,
                        Signed(duel.DeltaA), LeagueLabel(duel.RatingA + duel.DeltaA, t));
                    var rowB = string.Format(t.RatingRow, duel.BName, duel.RatingB, duel.RatingB + duel.DeltaB,
                        Signed(duel.DeltaB), LeagueLabel(duel.RatingB + duel.DeltaB, t));
                    text = string.Format(t.Finished, wName, lName, score, bo, aWon ? rowA : rowB, aWon ? rowB : rowA);
                }
                else
                {
                    text = string.Format(t.Finished, wName, lName, score, bo, t.FinishedUnrated, "").TrimEnd();
                }
                rows.Add([new BotButton(t.BtnChallenge, SwitchInline: "duel"), new BotButton(t.BtnLeague, Url: "startapp:duel")]);
                return (text + t.Footer, rows);
            }
            case DuelState.Expired:
                rows.Add([new BotButton(t.BtnLeague, Url: "startapp:duel")]);
                return (string.Format(t.Expired, duel.AName, duel.BName, $"{duel.ScoreA}:{duel.ScoreB}") + t.Footer, rows);
            default:
                rows.Add([new BotButton(t.BtnLeague, Url: "startapp:duel")]);
                return (string.Format(t.Cancelled, duel.AName, duel.BName) + t.Footer, rows);
        }
    }

    /* ---------------- Для Mini App ---------------- */

    public record ProfileView(
        string Name, string Tag, int Rating, int Peak, string League, int LeagueIndex, int? NextFloor, int Floor,
        int Games, int Wins, int Losses, int Rank, bool HasLink);

    public record DuelRowView(
        int Id, string State, int BestOf, string AName, string ATag, string BName, string BTag,
        int ScoreA, int ScoreB, int DeltaA, int DeltaB, bool Rated, DateTime AcceptedUtc, DateTime? FinishedUtc);

    public record TopRowView(int Rank, string Name, string Tag, int Rating, string League, int Wins, int Losses, bool Me);

    public record LeagueView(
        bool Linked, ProfileView? Me, DuelRowView? Active, List<DuelRowView> Mine, List<TopRowView> Top,
        List<DuelRowView> Recent, int Players, int[] Floors, string[] Leagues);

    public async Task<LeagueView> GetViewAsync(long telegramUserId, CancellationToken ct = default)
    {
        var linked = await players.GetByTelegramIdAsync(telegramUserId, ct) is not null;
        var me = await duels.GetProfileAsync(telegramUserId, ct);
        var top = await duels.GetTopAsync(100, ct);
        var recent = await duels.GetRecentAsync(10, ct);
        var count = await duels.CountProfilesAsync(ct);

        ProfileView? meView = null;
        DuelRowView? active = null;
        var mine = new List<DuelRowView>();
        if (me is not null)
        {
            var i = LeagueIndex(me.Rating);
            meView = new ProfileView(me.Name, me.PlayerTag, me.Rating, me.Peak, LeagueKeys[i], i,
                i + 1 < LeagueFloors.Length ? LeagueFloors[i + 1] : null, LeagueFloors[i],
                me.Games, me.Wins, me.Losses, await RankOfAsync(me, ct), !string.IsNullOrEmpty(me.FriendLink));
            var list = await duels.GetRecentForUserAsync(telegramUserId, 15, ct);
            mine = list.Where(d => d.State != DuelState.Active).Select(Row).ToList();
            if (list.FirstOrDefault(d => d.State == DuelState.Active) is { } act) active = Row(act);
        }

        var topRows = top.Select((p, n) => new TopRowView(n + 1, p.Name, p.PlayerTag, p.Rating,
            LeagueKeys[LeagueIndex(p.Rating)], p.Wins, p.Losses, p.TelegramUserId == telegramUserId)).ToList();

        return new LeagueView(linked, meView, active, mine, topRows, recent.Select(Row).ToList(), count,
            LeagueFloors, LeagueKeys);
    }

    private static DuelRowView Row(Duel d) => new(d.Id, d.State.ToString().ToLowerInvariant(), d.BestOf,
        d.AName, d.ATag, d.BName, d.BTag, d.ScoreA, d.ScoreB, d.DeltaA, d.DeltaB, d.Rated, d.AcceptedUtc, d.FinishedUtc);

    /* ---------------- Мелочи ---------------- */

    public static string Signed(int v) => v > 0 ? $"+{v}" : v < 0 ? $"−{-v}" : "±0";

    private static string Short(string name) => name.Length > 14 ? name[..13] + "…" : name;

    private static string Cut(string? s, int max) => s is null ? "" : s.Length > max ? s[..max] : s;

    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    private static string ToBase36(long v)
    {
        if (v <= 0) return "0";
        var chars = new Stack<char>();
        while (v > 0) { chars.Push(Digits[(int)(v % 36)]); v /= 36; }
        return new string(chars.ToArray());
    }

    private static long? FromBase36(string s)
    {
        if (s.Length is 0 or > 10) return null;
        long v = 0;
        foreach (var c in s)
        {
            var d = Digits.IndexOf(c);
            if (d < 0) return null;
            v = v * 36 + d;
        }
        return v;
    }
}
