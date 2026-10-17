using System.Text;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Авторская колонка канала: раз в день нейросеть пишет пост с мнением - не пересказ
/// цифр, а взгляд «автора, который сам играет»: о свежей новости или обновлении, о мете,
/// о колоде топ-1, об игроке дня. К посту - картинка из наших же генераторов
/// (/api/img: связи карт, колода, профиль) или обложка новости; у ролика YouTube это
/// превью, которое играет прямо в Telegram.
///
/// Факты - только из данных бота (ChannelAiUseCase.DataAsync): модель пишет мнение, но не
/// выдумывает цифры и ники. По умолчанию колонка ложится черновиком; с рубильником
/// «columnauto» уходит в канал сама.
/// </summary>
public class ChannelColumnUseCase(
    ChannelAiUseCase ai,
    ChannelUseCase channel,
    INewsTranslator translator,
    IChannelPostRepository posts,
    ITopPlayerRepository top,
    IClashRoyaleApi crApi,
    ICardUrls cardUrls,
    IServiceSettingRepository settings)
{
    private const int TzOffsetMinutes = 180;
    /// <summary>С какого часа по Киеву: днём, между новостями утра и вечерним «топом дня».</summary>
    private const int FromHourLocal = 14;
    private const int ToHourLocal = 21;
    private const string AttemptsKey = "channel.column.attempts";
    private const string LastNewsKey = "channel.column.lastnews";

    /// <summary>
    /// Голос колонки. Главное - чтобы читалось как пост живого человека, а не сводка:
    /// мнение, разговорный тон, без штампов, по которым текст нейросети узнают сразу.
    /// </summary>
    public const string Voice = """
        Напиши АВТОРСКИЙ пост от первого лица - как живой автор канала, который сам много играет в Clash Royale.
        Не пересказ цифр, а мнение: что это значит для обычного игрока, стоит ли пробовать, что радует или бесит, твой вывод.
        Пиши как человек в Telegram: короткие абзацы по 1-3 предложения, разговорно, можно игровой сленг
        (катка, ладдер, апнуть, контра, тильт), 0-2 эмодзи на весь текст кроме заголовка.
        Не используй штампы: «в мире Clash Royale», «давайте разберёмся», «не секрет, что», «погрузимся», «итак»,
        «стоит отметить», «в заключение», «важно понимать». Не ставь восклицательный знак в каждом предложении.
        Не называй себя ботом, нейросетью или ИИ. Цифры, ники и карты - только из данных, 1-3 самые показательные.
        Закончи коротким вопросом к читателям. Длина 500-900 символов.
        """;

    public enum Outcome { Ok, NoAi, NoChannel, NothingToWrite, Failed }

    /// <summary>Такт воркера: колонка раз в день в дневные часы, если рубильник включён.</summary>
    public async Task<bool> TickAsync(CancellationToken ct = default)
    {
        if (!translator.Enabled || !await channel.OnAsync("column", ct)) return false;
        var channelId = await channel.ChannelIdAsync(ct);
        if (channelId is null) return false;

        var now = DateTime.UtcNow;
        var local = now.AddMinutes(TzOffsetMinutes);
        if (local.Hour < FromHourLocal || local.Hour >= ToHourLocal) return false;
        var key = $"column:{local:yyyy-MM-dd}";
        if (await posts.ExistsAsync(key, ct)) return false;
        // Не вышло (нейросеть молчит, данных нет) - не больше двух попыток в день: это платные запросы
        if (!await DailyAttempts.TryStartAsync(settings, AttemptsKey, local.ToString("yyyy-MM-dd"), 2, ct)) return false;

        var post = await BuildAsync(now, ct);
        if (post is null) return false;
        post.SourceKey = key;
        if (!await posts.TryAddAsync(post, ct)) return false;
        if (await channel.OnAsync("columnauto", ct)) await channel.PublishPostAsync(post, channelId.Value, ct);
        return true;
    }

    /// <summary>Колонка сейчас, черновиком - кнопка в панели «Написать колонку».</summary>
    public async Task<(Outcome Outcome, string? Error)> DraftNowAsync(CancellationToken ct = default)
    {
        if (!translator.Enabled) return (Outcome.NoAi, null);
        var post = await BuildAsync(DateTime.UtcNow, ct);
        if (post is null) return (Outcome.Failed, translator.LastError ?? "Нейросеть не написала колонку - попробуй ещё раз");
        await posts.TryAddAsync(post, ct);
        return (Outcome.Ok, null);
    }

    /// <param name="NewsId">Новость, по которой пишем: запоминаем, чтобы завтра не взять её снова.</param>
    private record Topic(string Instruction, string? Extra, string? Image, string Button, string ButtonUrl, int? NewsId = null);

    /// <summary>
    /// Тема дня - по кругу: новость, мета, колода топ-1, игрок дня. Нет данных для темы
    /// (свежих новостей нет, снимка топа нет) - берём следующую.
    /// </summary>
    public async Task<ChannelPost?> BuildAsync(DateTime nowUtc, CancellationToken ct)
    {
        var builders = new Func<Task<Topic?>>[]
        {
            () => NewsTopicAsync(nowUtc, ct),
            () => MetaTopicAsync(nowUtc, ct),
            () => DeckTopicAsync(ct),
            () => PlayerTopicAsync(ct),
        };
        var start = DateOnly.FromDateTime(nowUtc.AddMinutes(TzOffsetMinutes)).DayNumber % builders.Length;

        string? data = null;
        for (var i = 0; i < builders.Length; i++)
        {
            Topic? topic;
            try { topic = await builders[(start + i) % builders.Length](); }
            catch (OperationCanceledException) { throw; }
            catch { continue; }
            if (topic is null) continue;

            data ??= await ai.DataAsync(ct);
            var request = $"{Voice}\n\nТема поста: {topic.Instruction}";
            var fullData = topic.Extra is null ? data : $"{data}\n== Материал для поста ==\n{topic.Extra}";
            var result = await translator.ComposeAsync(request, fullData, ct);
            if (result is not { } r || r.Text.Length == 0 || r.Text.StartsWith("⚠️", StringComparison.Ordinal)) continue;

            if (topic.NewsId is int newsId) await settings.SetAsync(LastNewsKey, newsId.ToString(), ct);
            return new ChannelPost
            {
                Kind = "column",
                State = ChannelPostState.Draft,
                Title = Cut(r.Title.Length > 0 ? r.Title : "Колонка", 200),
                Text = r.Text,
                PhotoUrl = topic.Image is { Length: <= 500 } img && img.StartsWith("http") ? img : null,
                ButtonText = topic.Button,
                ButtonUrl = topic.ButtonUrl,
                CreatedUtc = nowUtc,
            };
        }
        return null;
    }

    /* ---------------- Темы ---------------- */

    /// <summary>Мнение о свежей новости или обновлении: последняя новость за 4 дня, ещё не взятая в колонку.</summary>
    private async Task<Topic?> NewsTopicAsync(DateTime nowUtc, CancellationToken ct)
    {
        var lastId = int.TryParse(await settings.GetAsync(LastNewsKey, ct), out var v) ? v : 0;
        var news = (await posts.ListAsync([ChannelPostState.Published, ChannelPostState.Draft], 40, ct))
            .Where(p => p.Kind == "news" && p.Id != lastId && p.CreatedUtc >= nowUtc.AddDays(-4))
            .OrderByDescending(p => p.State == ChannelPostState.Published)
            .ThenByDescending(p => p.CreatedUtc)
            .FirstOrDefault();
        if (news is null) return null;
        return new Topic(
            "твоё мнение об этой новости или обновлении игры: что это изменит в ладдере и в КВ, кому выгодно, как к этому отнестись.",
            $"Заголовок: {news.Title}\n{StripLinks(news.Text)}",
            news.PhotoUrl,
            ChannelUseCase.DefaultButton, "startapp:",
            news.Id);
    }

    /// <summary>Мета недели: связи карт топа и растущие/падающие карты. Картинка - инфографика связей.</summary>
    private async Task<Topic?> MetaTopicAsync(DateTime nowUtc, CancellationToken ct)
    {
        var links = await channel.BuildLinksAsync(nowUtc, ct);
        if (links is null) return null;
        return new Topic(
            "твой взгляд на мету недели у топ-игроков: какие связки карт сейчас в моде, что из этого брать в обычный ладдер, а что переоценено.",
            StripLinks(links.Text),
            links.PhotoUrl,
            "Конструктор колод", "startapp:builder");
    }

    /// <summary>Колода игрока №1 мирового топа: картинка - его колода.</summary>
    private async Task<Topic?> DeckTopicAsync(CancellationToken ct)
    {
        var days = await top.DaysAsync(1, ct);
        if (days.Count == 0) return null;
        var first = (await top.GetDayAsync(days[0], ct)).OrderBy(p => p.Rank)
            .FirstOrDefault(p => !string.IsNullOrEmpty(p.DeckCardIds));
        if (first is null) return null;
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var cards = first.DeckCardIds!.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) && catalog.TryGetValue(Math.Abs(id), out var c) ? c.Name : null)
            .Where(n => n is not null)
            .ToList();
        if (cards.Count < 8) return null;
        return new Topic(
            "разбери колоду игрока №1 мирового топа: на чём она играет, кому подойдёт в обычном ладдере, чем её контрить.",
            $"Игрок №{first.Rank}: {first.Name}{(string.IsNullOrWhiteSpace(first.ClanName) ? "" : $" ({first.ClanName})")}, {first.Trophies}.\nКолода: {string.Join(", ", cards)}.",
            cardUrls.Card("deck", first.PlayerTag),
            "Мировой топ", "startapp:meta");
    }

    /// <summary>Игрок дня: самый большой взлёт в мировом топе за сутки. Картинка - его профиль.</summary>
    private async Task<Topic?> PlayerTopicAsync(CancellationToken ct)
    {
        var days = await top.DaysAsync(2, ct);
        if (days.Count < 2) return null;
        var today = await top.GetDayAsync(days[0], ct);
        var prev = (await top.GetDayAsync(days[1], ct)).ToDictionary(p => p.PlayerTag, p => p);
        var best = today
            .Where(p => prev.ContainsKey(p.PlayerTag))
            .Select(p => (p, Up: prev[p.PlayerTag].Rank - p.Rank))
            .Where(x => x.Up >= 20)
            .OrderByDescending(x => x.Up)
            .FirstOrDefault();
        if (best.p is null) return null;
        var p = best.p;
        return new Topic(
            "игрок дня из мирового топа: кто это, какой рывок он сделал, что у него можно подсмотреть обычному игроку.",
            $"{p.Name}{(string.IsNullOrWhiteSpace(p.ClanName) ? "" : $" ({p.ClanName})")}: за сутки с #{prev[p.PlayerTag].Rank} на #{p.Rank}, {p.Trophies}.",
            cardUrls.Card("profile", p.PlayerTag),
            "Мировой топ", "startapp:meta");
    }

    /// <summary>Ссылки [текст](url) → текст: в материал для модели адреса не нужны.</summary>
    private static string StripLinks(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
