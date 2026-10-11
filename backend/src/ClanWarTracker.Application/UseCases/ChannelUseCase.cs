using System.Text.Json;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Канал бота: автопосты из наших данных (мировой топ дня, неделя лиги 1×1, старт и
/// итоги челленджа, новые карты в игре), новости Clash Royale из лент и ручные посты
/// владельца.
///
/// Свои данные публикуются сразу - они проверены, их считал сам бот. Чужие новости
/// по умолчанию ждут владельца черновиком: лента может отдать мем, слух или рекламу,
/// и одна такая публикация от имени бота стоит дороже десятка пропущенных новостей.
///
/// Дедуп - уникальный SourceKey поста («daily:2026-10-11»): запись поста и есть
/// отметка «уже сделано», поэтому ни рестарт воркера, ни вторая его копия не
/// публикуют одно и то же дважды.
/// </summary>
public class ChannelUseCase(
    IServiceSettingRepository settings,
    IChannelPostRepository posts,
    IChannelPublisher publisher,
    ITopPlayerRepository top,
    IDuelRepository duels,
    ChallengeUseCase challenge,
    IClashRoyaleApi crApi,
    INewsFeedReader feeds,
    INewsTranslator translator)
{
    public const string KeyId = "channel.id";
    public const string KeyTitle = "channel.title";
    public const string KeyUsername = "channel.username";
    public const string KeyFeeds = "channel.feeds";
    public const string KeyFeedStatus = "channel.feedstatus";
    public const string KeyKnownCards = "channel.cards.known";
    private const string TogglePrefix = "channel.auto.";

    /// <summary>Официальный YouTube Clash Royale: трейлеры обнов, баланс, события.</summary>
    public const string DefaultFeed = "https://www.youtube.com/feeds/videos.xml?channel_id=UC_F8DoJf9MZogEOU51TpTbQ";

    /// <summary>Рубильники автопостов. newsauto - публиковать новости без проверки (только пересказанные).</summary>
    public static readonly string[] Toggles = ["daily", "weekly", "challenge", "cards", "news", "newsauto"];
    private static readonly HashSet<string> OffByDefault = ["newsauto"];

    /// <summary>Посты - по Киеву, как и остальные расписания бота.</summary>
    private const int TzOffsetMinutes = 180;
    private const int DailyHour = 19;
    private const int WeeklyHour = 12;

    /// <summary>Новости старше этого не берём: первое включение ленты не должно вывалить архив.</summary>
    private static readonly TimeSpan NewsWindow = TimeSpan.FromDays(4);
    private const int NewsPerFeedPerRun = 5;

    private static DateTime _lastNewsUtc = DateTime.MinValue;
    private static DateTime _lastCardsUtc = DateTime.MinValue;
    private static readonly TimeSpan NewsInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CardsInterval = TimeSpan.FromHours(6);

    public const string DefaultButton = "Открыть Clanify";

    /* ---------------- Настройки ---------------- */

    public record FeedStatus(string Url, bool Ok, int Items, string? Error, DateTime AtUtc);

    public record PostDto(int Id, string Kind, string State, string? Title, string Text, string? PhotoUrl, string? LinkUrl,
        string? ButtonText, string? ButtonUrl, DateTime CreatedUtc, DateTime? PublishedUtc, string? PostUrl, string? Error);

    public record StateDto(long? Id, string? Title, string? Username, Dictionary<string, bool> Toggles, List<string> Feeds,
        List<FeedStatus> FeedStatus, bool Translator, List<PostDto> Drafts, List<PostDto> Recent);

    public async Task<long?> ChannelIdAsync(CancellationToken ct) =>
        long.TryParse(await settings.GetAsync(KeyId, ct), out var id) && id != 0 ? id : null;

    public async Task<bool> OnAsync(string toggle, CancellationToken ct)
    {
        var v = await settings.GetAsync(TogglePrefix + toggle, ct);
        return string.IsNullOrEmpty(v) ? !OffByDefault.Contains(toggle) : v == "on";
    }

    public async Task<List<string>> FeedsAsync(CancellationToken ct)
    {
        var raw = await settings.GetAsync(KeyFeeds, ct);
        if (raw is null) return [DefaultFeed];
        return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public async Task<StateDto> GetAsync(CancellationToken ct = default)
    {
        var id = await ChannelIdAsync(ct);
        var username = await settings.GetAsync(KeyUsername, ct);
        var toggles = new Dictionary<string, bool>();
        foreach (var t in Toggles) toggles[t] = await OnAsync(t, ct);
        List<FeedStatus> status = [];
        try { status = JsonSerializer.Deserialize<List<FeedStatus>>(await settings.GetAsync(KeyFeedStatus, ct) ?? "[]") ?? []; }
        catch (JsonException) { }

        var drafts = await posts.ListAsync([ChannelPostState.Draft, ChannelPostState.Failed], 40, ct);
        var recent = await posts.ListAsync([ChannelPostState.Published], 20, ct);
        return new StateDto(id, await settings.GetAsync(KeyTitle, ct), string.IsNullOrEmpty(username) ? null : username,
            toggles, await FeedsAsync(ct), status, translator.Enabled,
            drafts.Select(p => Dto(p, id, username)).ToList(),
            recent.OrderByDescending(p => p.PublishedUtc).Select(p => Dto(p, id, username)).ToList());
    }

    private static PostDto Dto(ChannelPost p, long? channelId, string? username)
    {
        string? url = null;
        if (p.MessageId is int mid)
        {
            if (!string.IsNullOrEmpty(username)) url = $"https://t.me/{username}/{mid}";
            else if (channelId is long cid && cid.ToString().StartsWith("-100")) url = $"https://t.me/c/{cid.ToString()[4..]}/{mid}";
        }
        return new PostDto(p.Id, p.Kind, p.State.ToString().ToLowerInvariant(), p.Title, p.Text, p.PhotoUrl, p.LinkUrl,
            p.ButtonText, p.ButtonUrl, p.CreatedUtc, p.PublishedUtc, url, p.Error);
    }

    public enum ConnectResult { Ok, NotFound, NotAdmin }

    public async Task<ConnectResult> ConnectAsync(string handle, CancellationToken ct = default)
    {
        var info = await publisher.ResolveAsync(handle, ct);
        if (info is null) return ConnectResult.NotFound;
        if (!info.CanPost) return ConnectResult.NotAdmin;
        await settings.SetAsync(KeyId, info.Id.ToString(), ct);
        await settings.SetAsync(KeyTitle, info.Title, ct);
        await settings.SetAsync(KeyUsername, info.Username ?? "", ct);
        return ConnectResult.Ok;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await settings.SetAsync(KeyId, "", ct);
        await settings.SetAsync(KeyTitle, "", ct);
        await settings.SetAsync(KeyUsername, "", ct);
    }

    public async Task SaveSettingsAsync(IReadOnlyDictionary<string, bool>? toggles, IReadOnlyList<string>? feedList,
        CancellationToken ct = default)
    {
        foreach (var (k, v) in toggles ?? new Dictionary<string, bool>())
            if (Toggles.Contains(k)) await settings.SetAsync(TogglePrefix + k, v ? "on" : "off", ct);
        if (feedList is not null)
        {
            var clean = feedList
                .Select(f => f.Trim())
                .Where(f => Uri.TryCreate(f, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
            await settings.SetAsync(KeyFeeds, string.Join('\n', clean), ct);
        }
    }

    /* ---------------- Публикация ---------------- */

    /// <summary>Публикует уже записанный пост и отмечает итог в нём же.</summary>
    private async Task<bool> PublishAsync(ChannelPost post, long channelId, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<BotButton>>();
        if (!string.IsNullOrWhiteSpace(post.ButtonText) && !string.IsNullOrWhiteSpace(post.ButtonUrl))
            rows.Add(new[] { new BotButton(post.ButtonText.Trim(), Url: post.ButtonUrl.Trim()) });

        var id = await publisher.PublishAsync(channelId, ChannelText.ToHtml(post.Text), post.PhotoUrl, rows, ct);
        if (id is null && post.PhotoUrl is not null)
        {
            // Telegram не смог скачать картинку - пост без неё лучше, чем никакого
            id = await publisher.PublishAsync(channelId, ChannelText.ToHtml(post.Text), null, rows, ct);
        }
        post.MessageId = id;
        post.State = id is null ? ChannelPostState.Failed : ChannelPostState.Published;
        post.PublishedUtc = id is null ? null : DateTime.UtcNow;
        post.Error = id is null ? "Telegram не принял пост: проверьте, что бот - админ канала с правом публикации" : null;
        await posts.SaveChangesAsync(ct);
        return id is not null;
    }

    public enum PostResult { Ok, NoChannel, Empty, NotFound, Failed }

    /// <summary>Ручной пост владельца - сразу в канал.</summary>
    public async Task<PostResult> ComposeAsync(string? text, string? photoUrl, string? buttonText, string? buttonUrl,
        CancellationToken ct = default)
    {
        var channelId = await ChannelIdAsync(ct);
        if (channelId is null) return PostResult.NoChannel;
        if (string.IsNullOrWhiteSpace(text)) return PostResult.Empty;
        var post = new ChannelPost
        {
            Kind = "manual",
            State = ChannelPostState.Draft,
            Title = FirstLine(text),
            Text = text.Trim(),
            PhotoUrl = CleanUrl(photoUrl),
            ButtonText = string.IsNullOrWhiteSpace(buttonText) ? null : buttonText.Trim(),
            ButtonUrl = string.IsNullOrWhiteSpace(buttonText) ? null : CleanButtonUrl(buttonUrl),
            CreatedUtc = DateTime.UtcNow,
        };
        await posts.TryAddAsync(post, ct);
        return await PublishAsync(post, channelId.Value, ct) ? PostResult.Ok : PostResult.Failed;
    }

    /// <summary>Опубликовать черновик (или упавший пост), по желанию с правками владельца.</summary>
    public async Task<PostResult> PublishDraftAsync(int id, string? text, string? photoUrl, CancellationToken ct = default)
    {
        var channelId = await ChannelIdAsync(ct);
        if (channelId is null) return PostResult.NoChannel;
        var post = await posts.GetAsync(id, ct);
        if (post is null || post.State == ChannelPostState.Published) return PostResult.NotFound;
        if (text is not null)
        {
            if (string.IsNullOrWhiteSpace(text)) return PostResult.Empty;
            post.Text = text.Trim();
        }
        if (photoUrl is not null) post.PhotoUrl = CleanUrl(photoUrl);
        return await PublishAsync(post, channelId.Value, ct) ? PostResult.Ok : PostResult.Failed;
    }

    public async Task<bool> RejectAsync(int id, CancellationToken ct = default)
    {
        var post = await posts.GetAsync(id, ct);
        if (post is null || post.State == ChannelPostState.Published) return false;
        post.State = ChannelPostState.Rejected;
        await posts.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Собрать автопост прямо сейчас черновиком - посмотреть, как он выглядит, не
    /// дожидаясь расписания. В канал не уходит, пока владелец не нажмёт «Опубликовать».
    /// </summary>
    public async Task<bool> PreviewAsync(string kind, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var post = kind switch
        {
            "daily" => await BuildDailyAsync(now, ct),
            "weekly" => await BuildWeeklyAsync(now, ct),
            _ => null,
        };
        if (post is null) return false;
        post.State = ChannelPostState.Draft;
        post.SourceKey = null;
        return await posts.TryAddAsync(post, ct);
    }

    private static string? FirstLine(string text)
    {
        var line = text.Trim().Split('\n')[0].Replace("**", "").Trim();
        return line.Length <= 120 ? line : line[..120] + "…";
    }

    private static string? CleanUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")
            ? u.ToString()
            : null;

    /// <summary>Кнопка ведёт либо в раздел приложения (startapp:…), либо на обычную ссылку.</summary>
    private static string? CleanButtonUrl(string? url)
    {
        var u = url?.Trim() ?? "";
        if (u.Length == 0) return "startapp:";
        if (u.StartsWith("startapp:", StringComparison.Ordinal) && u.Length <= 80) return u;
        return CleanUrl(u);
    }

    /* ---------------- Расписание ---------------- */

    public record TickResult(int Published, int Drafts);

    /// <summary>Такт воркера: что созрело - публикуем. Без подключённого канала - ничего.</summary>
    public async Task<TickResult> TickAsync(CancellationToken ct = default)
    {
        var channelId = await ChannelIdAsync(ct);
        if (channelId is null) return new TickResult(0, 0);
        var now = DateTime.UtcNow;
        var local = now.AddMinutes(TzOffsetMinutes);
        int published = 0, drafts = 0;

        async Task Auto(string key, Func<Task<ChannelPost?>> build)
        {
            if (await posts.ExistsAsync(key, ct)) return;
            var post = await build();
            if (post is null) return; // данных ещё нет - попробуем в следующий такт
            post.SourceKey = key;
            post.State = ChannelPostState.Draft;
            if (!await posts.TryAddAsync(post, ct)) return; // другой проход успел первым
            if (await PublishAsync(post, channelId.Value, ct)) published++;
        }

        if (local.Hour >= DailyHour && local.Hour < 23 && await OnAsync("daily", ct))
            await Auto($"daily:{local:yyyy-MM-dd}", () => BuildDailyAsync(now, ct));

        if (local.DayOfWeek == DayOfWeek.Monday && local.Hour >= WeeklyHour && local.Hour < 20 && await OnAsync("weekly", ct))
            await Auto($"weekly:{local:yyyy-MM-dd}", () => BuildWeeklyAsync(now, ct));

        if (await OnAsync("challenge", ct))
        {
            var e = await challenge.CurrentAsync(ct);
            if (now >= e.StartUtc && now < e.StartUtc.AddHours(6) && now < e.EndUtc)
                await Auto($"chstart:{e.Id}", () => Task.FromResult<ChannelPost?>(BuildChallengeStart(e)));
            // Сорок пять минут после конца - бот успевает дочитать последние бои
            if (now >= e.EndUtc.AddMinutes(45) && now < e.EndUtc.AddDays(1))
                await Auto($"chend:{e.Id}", () => BuildChallengeEndAsync(e, ct));
        }

        if (now - _lastCardsUtc >= CardsInterval && await OnAsync("cards", ct))
        {
            _lastCardsUtc = now;
            foreach (var post in await NewCardsAsync(ct))
            {
                var key = post.SourceKey!;
                post.SourceKey = null;
                await Auto(key, () => Task.FromResult<ChannelPost?>(post));
            }
        }

        if (now - _lastNewsUtc >= NewsInterval && await OnAsync("news", ct))
        {
            _lastNewsUtc = now;
            var r = await FetchNewsAsync(channelId.Value, ct);
            published += r.Published;
            drafts += r.Drafts;
        }

        return new TickResult(published, drafts);
    }

    /* ---------------- Мировой топ дня ---------------- */

    public async Task<ChannelPost?> BuildDailyAsync(DateTime nowUtc, CancellationToken ct)
    {
        var local = nowUtc.AddMinutes(TzOffsetMinutes);
        var lines = new List<string?> { $"🌍 **Мировой топ · {ChannelText.Day(local)}**", "" };
        var hasTop = false;

        var days = await top.DaysAsync(2, ct);
        // Снимок не старше суток: вчерашний топ под сегодняшней датой был бы враньём
        if (days.Count > 0 && string.CompareOrdinal(days[0], nowUtc.AddDays(-1).ToString("yyyy-MM-dd")) >= 0)
        {
            var today = (await top.GetDayAsync(days[0], ct)).OrderBy(p => p.Rank).ToList();
            if (today.Count >= 3)
            {
                hasTop = true;
                foreach (var p in today.Take(3))
                    lines.Add($"{(p.Rank == 1 ? "👑" : ChannelText.Medal(p.Rank))} {ChannelText.Plain(p.Name)}"
                              + (string.IsNullOrWhiteSpace(p.ClanName) ? "" : $" · {ChannelText.Plain(p.ClanName)}")
                              + $" - {ChannelText.Num(p.Trophies)} 🏆");
                lines.Add("");

                if (days.Count > 1)
                {
                    var prev = (await top.GetDayAsync(days[1], ct)).ToDictionary(p => p.PlayerTag, p => p);
                    var climber = today
                        .Where(p => prev.ContainsKey(p.PlayerTag))
                        .Select(p => (p, Up: prev[p.PlayerTag].Rank - p.Rank))
                        .Where(x => x.Up >= 20)
                        .OrderByDescending(x => x.Up)
                        .FirstOrDefault();
                    if (climber.p is not null)
                        lines.Add($"📈 Взлёт дня: **{ChannelText.Plain(climber.p.Name)}** - с #{prev[climber.p.PlayerTag].Rank} на #{climber.p.Rank}");

                    var newcomers = today.Count(p => p.Rank <= 100
                        && (!prev.TryGetValue(p.PlayerTag, out var was) || was.Rank > 100));
                    if (newcomers > 0)
                        lines.Add($"🆕 Новых лиц в топ-100: {newcomers}");

                    var lastNow = today[^1];
                    var lastPrev = prev.Values.OrderByDescending(p => p.Rank).FirstOrDefault();
                    if (lastNow.Rank >= 900 && lastPrev is not null)
                    {
                        var diff = lastNow.Trophies - lastPrev.Trophies;
                        lines.Add($"🚪 Вход в топ-{lastNow.Rank}: {ChannelText.Num(lastNow.Trophies)} 🏆"
                                  + (diff != 0 ? $" ({ChannelText.Signed(diff)} за сутки)" : ""));
                    }
                }

                var cards = await TopCardsAsync(today.Where(p => p.Rank <= 100), ct);
                if (cards.Count > 0) lines.Add($"🃏 Чаще всего в колодах топ-100: {string.Join(", ", cards)}");
            }
        }

        // Лига 1×1 за сутки
        var dayDuels = (await duels.GetRecentAsync(300, ct))
            .Where(d => d.FinishedUtc >= nowUtc.AddDays(-1))
            .ToList();
        if (dayDuels.Count > 0)
        {
            lines.Add("");
            lines.Add($"⚔️ Лига 1×1 за сутки: {dayDuels.Count} {ChannelText.Plural(dayDuels.Count, "дуэль", "дуэли", "дуэлей")}");
            var best = BestGainer(dayDuels);
            if (best is { Gain: > 0 })
                lines.Add($"🔥 Игрок дня: **{ChannelText.Plain(best.Value.Name)}** +{best.Value.Gain} кубков лиги");
        }

        if (!hasTop && dayDuels.Count == 0) return null;
        return new ChannelPost
        {
            Kind = "daily",
            Title = $"Мировой топ · {ChannelText.Day(local)}",
            Text = ChannelText.Join(lines),
            ButtonText = hasTop ? "Мировой топ" : "Лига 1×1",
            ButtonUrl = hasTop ? "startapp:meta" : "startapp:duel",
            CreatedUtc = nowUtc,
        };
    }

    /// <summary>Самые частые карты в колодах - по именам из справочника игры.</summary>
    private async Task<List<string>> TopCardsAsync(IEnumerable<TopPlayer> players, CancellationToken ct)
    {
        var counts = players
            .Where(p => !string.IsNullOrEmpty(p.DeckCardIds))
            .SelectMany(p => p.DeckCardIds!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(id => id.Trim())
            .OrderByDescending(g => g.Count())
            .Take(3)
            .ToList();
        if (counts.Count == 0) return [];
        IReadOnlyDictionary<string, CrCatalogCard> catalog;
        try { catalog = await crApi.GetAllCardsAsync(ct); }
        catch { return []; }
        var byId = catalog.Values.GroupBy(c => c.Id.ToString()).ToDictionary(g => g.Key, g => g.First().Name);
        return counts.Select(g => byId.GetValueOrDefault(g.Key)).OfType<string>().ToList();
    }

    private static (string Name, int Gain)? BestGainer(IEnumerable<Duel> list)
    {
        var gains = new Dictionary<long, (string Name, int Gain)>();
        foreach (var d in list.Where(d => d.Rated))
        {
            Add(d.ATelegramUserId, d.AName, d.DeltaA);
            Add(d.BTelegramUserId, d.BName, d.DeltaB);
        }
        return gains.Count == 0 ? null : gains.Values.OrderByDescending(g => g.Gain).First();

        void Add(long id, string name, int delta) =>
            gains[id] = (name, (gains.TryGetValue(id, out var g) ? g.Gain : 0) + delta);
    }

    /* ---------------- Неделя лиги 1×1 ---------------- */

    public async Task<ChannelPost?> BuildWeeklyAsync(DateTime nowUtc, CancellationToken ct)
    {
        var leaders = await duels.GetTopAsync(5, ct);
        if (leaders.Count < 3) return null;
        var week = (await duels.GetRecentAsync(1000, ct)).Where(d => d.FinishedUtc >= nowUtc.AddDays(-7)).ToList();

        var lines = new List<string?> { "⚔️ **Лига 1×1 · итоги недели**", "" };
        var rank = 0;
        foreach (var p in leaders)
        {
            rank++;
            lines.Add($"{ChannelText.Medal(rank)} {ChannelText.Plain(p.Name)} - {ChannelText.Num(p.Rating)} · {DuelUseCase.LeagueLabel(p.Rating, DuelText.Ru)}");
        }
        lines.Add("");
        lines.Add($"Дуэлей за неделю: {ChannelText.Num(week.Count)}");
        var best = BestGainer(week);
        if (best is { Gain: > 0 })
            lines.Add($"📈 Рывок недели: **{ChannelText.Plain(best.Value.Name)}** +{best.Value.Gain} кубков лиги");
        lines.Add("");
        lines.Add("Вызови любого игрока прямо в чате - бот сам засчитает счёт по журналу боёв.");

        return new ChannelPost
        {
            Kind = "weekly",
            Title = "Лига 1×1 · итоги недели",
            Text = ChannelText.Join(lines),
            ButtonText = "Вызвать на дуэль",
            ButtonUrl = "startapp:duel",
            CreatedUtc = nowUtc,
        };
    }

    /* ---------------- Челлендж ---------------- */

    private static ChannelPost BuildChallengeStart(ChallengeUseCase.Event e)
    {
        var title = string.IsNullOrWhiteSpace(e.Title) ? "Челлендж выходных" : e.Title.Trim();
        var rule = ChallengeRules.Normalize(e.Rule);
        var end = e.EndUtc.AddMinutes(TzOffsetMinutes);
        var lines = new List<string?>
        {
            $"🏁 **Стартовал челлендж: {ChannelText.Plain(title)}**",
            "",
            ChannelText.RuleLine(rule),
            string.IsNullOrWhiteSpace(e.Prize) ? null : $"🎁 Приз: {ChannelText.Plain(e.Prize)}",
            $"⏳ До {ChannelText.Day(end)}, {end:HH:mm} по Киеву",
            "",
            "Вступай в боте - очки считаются сами по журналу боёв, скриншоты не нужны.",
        };
        return new ChannelPost
        {
            Kind = "chstart",
            Title = $"Старт: {title}",
            Text = ChannelText.Join(lines),
            ButtonText = "Участвовать",
            ButtonUrl = "startapp:challenge",
            CreatedUtc = DateTime.UtcNow,
        };
    }

    private async Task<ChannelPost?> BuildChallengeEndAsync(ChallengeUseCase.Event e, CancellationToken ct)
    {
        var rows = (await challenge.ResultsAsync(e, ct)).Where(r => r.Tickets > 0).OrderBy(r => r.Rank).ToList();
        if (rows.Count == 0) return null;
        var title = string.IsNullOrWhiteSpace(e.Title) ? "Челлендж выходных" : e.Title.Trim();
        var rule = ChallengeRules.Normalize(e.Rule);
        var lines = new List<string?> { $"🏆 **Итоги челленджа: {ChannelText.Plain(title)}**", "" };
        foreach (var r in rows.Take(3))
            lines.Add($"{ChannelText.Medal(r.Rank)} {ChannelText.Plain(r.Name)} - {ChannelText.Points(r.Tickets, rule)}");
        lines.Add("");
        var total = await challenge.ParticipantsAsync(e, ct);
        lines.Add($"Участников: {ChannelText.Num(total)}. Спасибо всем, кто играл!");
        if (!string.IsNullOrWhiteSpace(e.Prize))
            lines.Add($"🎁 Приз «{ChannelText.Plain(e.Prize)}» - победителю, с ним свяжемся.");
        return new ChannelPost
        {
            Kind = "chend",
            Title = $"Итоги: {title}",
            Text = ChannelText.Join(lines),
            ButtonText = "Таблица",
            ButtonUrl = "startapp:challenge",
            CreatedUtc = DateTime.UtcNow,
        };
    }

    /* ---------------- Новые карты ---------------- */

    /// <summary>
    /// Сверка справочника карт с тем, что видели раньше: новая карта или новая эволюция -
    /// пост. Первый запуск только запоминает справочник, иначе в канал ушли бы все сто
    /// с лишним карт игры. SourceKey поста здесь - ключ дедупа, Auto его переставит.
    /// </summary>
    private async Task<List<ChannelPost>> NewCardsAsync(CancellationToken ct)
    {
        IReadOnlyDictionary<string, CrCatalogCard> catalog;
        try { catalog = await crApi.GetAllCardsAsync(ct); }
        catch { return []; }
        if (catalog.Count < 50) return []; // API ответил огрызком - не повод объявлять «новые» карты

        var current = new HashSet<string>();
        foreach (var c in catalog.Values)
        {
            current.Add(c.Id.ToString());
            if (c.MaxEvolutionLevel > 0) current.Add(c.Id + ":e");
        }

        var raw = await settings.GetAsync(KeyKnownCards, ct);
        var known = (raw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        await settings.SetAsync(KeyKnownCards, string.Join(',', current.Union(known).OrderBy(x => x)), ct);
        if (known.Count == 0) return [];

        var fresh = current.Except(known).ToList();
        // Десяток «новых» разом - это не обнова, а перемена в API (новые башни, переименование)
        if (fresh.Count > 4) return [];

        var result = new List<ChannelPost>();
        foreach (var key in fresh)
        {
            var evo = key.EndsWith(":e");
            var id = evo ? key[..^2] : key;
            var card = catalog.Values.FirstOrDefault(c => c.Id.ToString() == id);
            if (card is null) continue;
            // Карта вышла сразу с эволюцией - один пост про карту, а не два
            if (evo && fresh.Contains(id)) continue;
            var rarity = ChannelText.Rarity(card.Rarity);
            var lines = evo
                ? new List<string?> { $"✨ **Новая эволюция: {ChannelText.Plain(card.Name)}**", "", "Эволюция уже в игре - пора пересобирать колоды." }
                : new List<string?>
                {
                    $"🆕 **В игре новая карта: {ChannelText.Plain(card.Name)}**",
                    "",
                    string.Join(" · ", new[] { rarity, card.ElixirCost > 0 ? $"{card.ElixirCost} эликсира" : "" }.Where(s => s.Length > 0)),
                };
            result.Add(new ChannelPost
            {
                Kind = "card",
                SourceKey = (evo ? "evo:" : "card:") + id,
                Title = (evo ? "Эволюция: " : "Новая карта: ") + card.Name,
                Text = ChannelText.Join(lines),
                PhotoUrl = evo ? card.EvoIconUrl ?? card.IconUrl : card.IconUrl,
                ButtonText = DefaultButton,
                ButtonUrl = "startapp:",
                CreatedUtc = DateTime.UtcNow,
            });
        }
        return result;
    }

    /* ---------------- Новости из лент ---------------- */

    /// <summary>
    /// Читает ленты: новое - в черновики (или сразу в канал, если владелец так решил и
    /// новость удалось пересказать). Вызывается и по таймеру, и кнопкой «Проверить сейчас».
    /// </summary>
    public async Task<TickResult> FetchNewsAsync(long? channelId = null, CancellationToken ct = default)
    {
        channelId ??= await ChannelIdAsync(ct);
        var auto = channelId is not null && await OnAsync("newsauto", ct) && translator.Enabled;
        var now = DateTime.UtcNow;
        int published = 0, drafts = 0;
        var status = new List<FeedStatus>();

        foreach (var feed in await FeedsAsync(ct))
        {
            List<NewsItem> items;
            try { items = await feeds.ReadAsync(feed, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                status.Add(new FeedStatus(feed, false, 0, ex.Message.Length > 120 ? ex.Message[..120] : ex.Message, now));
                continue;
            }
            status.Add(new FeedStatus(feed, true, items.Count, null, now));

            var taken = 0;
            foreach (var item in items.Where(i => i.PublishedUtc is DateTime d && now - d <= NewsWindow))
            {
                if (taken >= NewsPerFeedPerRun) break;
                var key = "news:" + (item.Key.Length > 290 ? item.Key[..290] : item.Key);
                if (await posts.ExistsAsync(key, ct)) continue;
                taken++;

                string title = item.Title, text;
                var retold = await translator.RetellAsync(item.Title, item.Summary, ct);
                if (retold is { } r)
                {
                    // Модель сочла запись не новостью об игре - запоминаем, чтобы не спрашивать снова
                    if (r.Title.Length == 0)
                    {
                        await posts.TryAddAsync(new ChannelPost
                        {
                            Kind = "news", State = ChannelPostState.Rejected, SourceKey = key, Title = item.Title,
                            Text = item.Title, LinkUrl = item.Link, CreatedUtc = now,
                        }, ct);
                        continue;
                    }
                    title = r.Title;
                    text = ChannelText.Join([$"**{ChannelText.Plain(r.Title)}**", "", r.Text, "", $"[Источник]({item.Link})"]);
                }
                else
                {
                    var summary = item.Summary is { Length: > 600 } s ? s[..600].TrimEnd() + "…" : item.Summary;
                    text = ChannelText.Join([$"**{ChannelText.Plain(item.Title)}**", "", ChannelText.Plain(summary), "", $"[Источник]({item.Link})"]);
                }

                var post = new ChannelPost
                {
                    Kind = "news",
                    State = ChannelPostState.Draft,
                    SourceKey = key,
                    Title = title.Length > 200 ? title[..200] : title,
                    Text = text,
                    PhotoUrl = item.ImageUrl is { Length: <= 500 } img && img.StartsWith("http") ? img : null,
                    LinkUrl = item.Link.Length <= 500 ? item.Link : null,
                    ButtonText = DefaultButton,
                    ButtonUrl = "startapp:",
                    CreatedUtc = now,
                };
                if (!await posts.TryAddAsync(post, ct)) continue;
                if (auto && retold is not null)
                {
                    if (await PublishAsync(post, channelId!.Value, ct)) published++;
                }
                else drafts++;
            }
        }

        await settings.SetAsync(KeyFeedStatus, JsonSerializer.Serialize(status), ct);
        return new TickResult(published, drafts);
    }
}
