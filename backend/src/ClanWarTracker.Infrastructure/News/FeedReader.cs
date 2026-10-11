using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// Источники новостей для канала. Понимает:
/// - RSS 2.0 и Atom: сайты, YouTube (youtube.com/feeds/videos.xml?channel_id=…), Reddit (.rss);
/// - обычную страницу блога без ленты (официальный блог Supercell): статьи - это ссылки
///   с этой страницы вглубь её же раздела.
///
/// Сама лента даёт только заголовок и пару строк анонса, поэтому для поста бот отдельно
/// читает страницу новости целиком (ReadArticleAsync). Адреса задаёт только владелец.
/// </summary>
public partial class FeedReader(HttpClient http) : INewsFeedReader
{
    /// <summary>Лента или статья больше трёх мегабайт - что-то не то по ссылке.</summary>
    private const int MaxBytes = 3_000_000;

    /// <summary>Сколько текста статьи отдаём на пересказ: больше не нужно и дороже.</summary>
    private const int MaxArticleChars = 7000;

    private const string UserAgent = "Mozilla/5.0 (compatible; ClanifyBot/1.0; +https://t.me/clanifybot)";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    public async Task<List<NewsItem>> ReadAsync(string feedUrl, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("bad feed url");

        // Reddit - не лента, а топ дня с апвоутами: по ним отсеивается неинтересное
        if (RedditRegex().Match(uri.Host + uri.AbsolutePath) is { Success: true } reddit)
            return await ReadRedditAsync(reddit.Groups[1].Value, ct);

        var text = await DownloadAsync(uri, ct);
        var head = text.Length > 2000 ? text[..2000] : text;
        var isFeed = head.Contains("<rss", StringComparison.OrdinalIgnoreCase)
                     || head.Contains("<feed", StringComparison.OrdinalIgnoreCase)
                     || head.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

        var items = isFeed ? ParseFeed(text) : ParseListingPage(uri, text);
        return items
            .Where(i => i.Title.Length > 0 && i.Link.Length > 0)
            .OrderByDescending(i => i.PublishedUtc ?? DateTime.MinValue)
            .Take(30)
            .ToList();
    }

    private async Task<string> DownloadAsync(Uri uri, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        // Reddit и часть сайтов отвечают 429/403 на запрос без внятного User-Agent
        req.Headers.UserAgent.ParseAdd(UserAgent);
        req.Headers.AcceptLanguage.ParseAdd("en");
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        if (resp.Content.Headers.ContentLength > MaxBytes) throw new InvalidOperationException("too large");

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes) throw new InvalidOperationException("too large");
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /* ---------------- RSS / Atom ---------------- */

    private static IEnumerable<NewsItem> ParseFeed(string text)
    {
        // DTD запрещены: внешние сущности в чужом XML - классическая дыра
        using var xml = XmlReader.Create(new StringReader(text),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(xml).Root ?? throw new InvalidOperationException("empty feed");
        return (root.Name == Atom + "feed" ? ParseAtom(root) : ParseRss(root)).ToList();
    }

    private static IEnumerable<NewsItem> ParseAtom(XElement feed)
    {
        foreach (var e in feed.Elements(Atom + "entry"))
        {
            var link = e.Elements(Atom + "link").FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")
                       ?? e.Element(Atom + "link");
            var href = (string?)link?.Attribute("href") ?? "";
            var group = e.Element(Media + "group");
            var videoId = (string?)e.Element(Yt + "videoId");
            var isVideo = videoId is { Length: > 0 };
            var summary = (string?)group?.Element(Media + "description")
                          ?? (string?)e.Element(Atom + "summary")
                          ?? (string?)e.Element(Atom + "content");
            var image = isVideo
                ? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"
                : (string?)group?.Element(Media + "thumbnail")?.Attribute("url")
                  ?? (string?)e.Element(Media + "thumbnail")?.Attribute("url")
                  ?? FirstImage(summary);
            yield return new NewsItem(
                Key: (string?)e.Element(Atom + "id") ?? href,
                Title: Clean((string?)e.Element(Atom + "title"), 300),
                Summary: isVideo ? VideoDescription(summary) : Clean(summary, 2000),
                Link: href,
                ImageUrl: image,
                PublishedUtc: Date((string?)e.Element(Atom + "published") ?? (string?)e.Element(Atom + "updated")),
                VideoId: isVideo ? videoId : null);
        }
    }

    private static IEnumerable<NewsItem> ParseRss(XElement root)
    {
        var channel = root.Element("channel") ?? root;
        foreach (var e in channel.Elements("item"))
        {
            var link = ((string?)e.Element("link") ?? "").Trim();
            var description = (string?)e.Element("description");
            var image = (string?)e.Element("enclosure")?.Attribute("url")
                        ?? (string?)e.Element(Media + "content")?.Attribute("url")
                        ?? (string?)e.Element(Media + "thumbnail")?.Attribute("url")
                        ?? FirstImage(description);
            yield return new NewsItem(
                Key: ((string?)e.Element("guid"))?.Trim() is { Length: > 0 } guid ? guid : link,
                Title: Clean((string?)e.Element("title"), 300),
                Summary: Clean(description, 2000),
                Link: link,
                ImageUrl: image,
                PublishedUtc: Date((string?)e.Element("pubDate")));
        }
    }

    /// <summary>
    /// Описание видео без шлака: ссылки на соцсети, хештеги, «подписывайся», таймкоды.
    /// Остаются строки, которые говорят, о чём видео.
    /// </summary>
    private static string VideoDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var lines = WebUtility.HtmlDecode(raw)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0
                        && !l.Contains("http", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith('#')
                        && !JunkLineRegex().IsMatch(l)
                        && !TimecodeRegex().IsMatch(l))
            .Take(6);
        var text = string.Join("\n", lines);
        return text.Length <= 1200 ? text : text[..1200].TrimEnd() + "…";
    }

    [GeneratedRegex(@"subscribe|follow us|download|social|facebook|instagram|tiktok|twitter|discord|merch|official site|^(?:\W*)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex JunkLineRegex();

    [GeneratedRegex(@"^\d{1,2}:\d{2}")]
    private static partial Regex TimecodeRegex();

    /* ---------------- Reddit ---------------- */

    [GeneratedRegex(@"^(?:www\.|old\.)?reddit\.com/r/([A-Za-z0-9_]{2,40})", RegexOptions.IgnoreCase)]
    private static partial Regex RedditRegex();

    /// <summary>
    /// Топ сабреддита за сутки из его JSON (та же публичная страница, что и в браузере):
    /// в RSS нет апвоутов, а без них интересное от шума не отличить. Закреплённые посты
    /// модераторов и 18+ не берём.
    /// </summary>
    private async Task<List<NewsItem>> ReadRedditAsync(string sub, CancellationToken ct)
    {
        var json = await DownloadAsync(new Uri($"https://www.reddit.com/r/{sub}/top.json?t=day&limit=25&raw_json=1"), ct);
        using var doc = JsonDocument.Parse(json);
        var list = new List<NewsItem>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("children", out var children))
            return list;
        foreach (var child in children.EnumerateArray())
        {
            if (!child.TryGetProperty("data", out var d)) continue;
            if (Bool(d, "stickied") || Bool(d, "over_18")) continue;
            var id = Str(d, "id");
            var title = Clean(Str(d, "title"), 300);
            var permalink = Str(d, "permalink");
            if (id is null || title.Length == 0 || permalink is null) continue;

            var hint = Str(d, "post_hint");
            var url = Str(d, "url_overridden_by_dest") ?? Str(d, "url");
            string? image = hint == "image" ? url : null;
            if (image is null && d.TryGetProperty("preview", out var preview)
                && preview.TryGetProperty("images", out var images) && images.GetArrayLength() > 0
                && images[0].TryGetProperty("source", out var source))
                image = Str(source, "url");

            // Текст поста; у поста-ссылки - куда он ведёт
            var body = Str(d, "selftext") is { Length: > 0 } self ? self
                : hint == "link" && url is not null ? "Ссылка: " + url : null;
            var flair = Str(d, "link_flair_text");
            var summary = string.Join("\n", new[] { flair is null ? null : "Рубрика: " + flair, body }.Where(x => x is not null));

            list.Add(new NewsItem(
                Key: "reddit:" + id,
                Title: title,
                Summary: summary.Length <= 2500 ? summary : summary[..2500] + "…",
                Link: "https://www.reddit.com" + permalink,
                ImageUrl: image,
                PublishedUtc: d.TryGetProperty("created_utc", out var created) && created.TryGetDouble(out var sec)
                    ? DateTimeOffset.FromUnixTimeSeconds((long)sec).UtcDateTime
                    : null,
                Score: d.TryGetProperty("score", out var score) && score.TryGetInt32(out var sc) ? sc : 0));
        }
        return list;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /* ---------------- Страница блога без ленты ---------------- */

    /// <summary>
    /// Статьи со страницы-списка: ссылки вглубь её же раздела со «сло́говым» адресом
    /// (balance-changes-october). Короткие адреса без дефиса - это рубрики и страницы
    /// навигации, их не берём. Даты на таких страницах нет: новое отличаем от старого
    /// по тому, видели ли ссылку раньше.
    /// </summary>
    private static IEnumerable<NewsItem> ParseListingPage(Uri page, string html)
    {
        var basePath = page.AbsolutePath.TrimEnd('/') + "/";
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AnchorRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            if (!Uri.TryCreate(page, href, out var abs)) continue;
            if (!string.Equals(abs.Host, page.Host, StringComparison.OrdinalIgnoreCase)) continue;
            var path = abs.AbsolutePath;
            if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) || path.Length <= basePath.Length) continue;
            var slug = path.TrimEnd('/').Split('/')[^1];
            if (slug.Length < 10 || !slug.Contains('-')) continue;

            var url = abs.GetLeftPart(UriPartial.Path);
            var title = Clean(m.Groups[2].Value, 300);
            if (title.Length < 6) title = Humanize(slug);
            if (!found.TryGetValue(url, out var known) || known.Length < title.Length) found[url] = title;
        }
        return found.Select(x => new NewsItem(x.Key, x.Value, null, x.Key, null, null)).Take(30);
    }

    private static string Humanize(string slug)
    {
        var words = slug.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        var s = string.Join(' ', words);
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }

    [GeneratedRegex("<a\\b[^>]*?href=[\"']([^\"'#]+)[\"'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    /* ---------------- Страница новости ---------------- */

    public async Task<NewsArticle?> ReadArticleAsync(NewsItem item, CancellationToken ct = default)
    {
        if (item.VideoId is { Length: > 0 } videoId)
        {
            // Обложка 1280×720 есть почти у всех роликов; hqdefault - запасная, с чёрными полосами
            var hd = $"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg";
            return new NewsArticle(null, null, null, await ExistsAsync(hd, ct) ? hd : item.ImageUrl);
        }
        // У поста Reddit текст уже есть, а страница обсуждения - это комментарии и меню
        if (item.IsReddit) return null;
        if (!Uri.TryCreate(item.Link, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            return null;

        string html;
        try { html = await DownloadAsync(uri, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }

        var image = Meta(html, "og:image") ?? Meta(html, "twitter:image");
        if (image is not null && Uri.TryCreate(uri, image, out var img)) image = img.ToString();
        var text = ArticleText(html);
        return new NewsArticle(
            Meta(html, "og:title"),
            Meta(html, "og:description") ?? Meta(html, "description"),
            text.Length >= 80 ? text : null,
            image);
    }

    private async Task<bool> ExistsAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; }
    }

    /// <summary>&lt;meta property|name="key" content="…"&gt; - в любом порядке атрибутов.</summary>
    private static string? Meta(string html, string key)
    {
        foreach (Match m in MetaRegex().Matches(html))
        {
            var tag = m.Value;
            var name = AttrRegex("property").Match(tag) is { Success: true } p ? p.Groups[1].Value
                : AttrRegex("name").Match(tag) is { Success: true } n ? n.Groups[1].Value : null;
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) continue;
            var content = AttrRegex("content").Match(tag);
            if (content.Success)
            {
                var v = WebUtility.HtmlDecode(content.Groups[1].Value).Trim();
                if (v.Length > 0) return v;
            }
        }
        return null;
    }

    private static Regex AttrRegex(string attr) =>
        new($"\\b{attr}\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);

    [GeneratedRegex("<meta\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRegex();

    /// <summary>
    /// Текст статьи: заголовки, абзацы и пункты списков из &lt;article&gt; (или &lt;main&gt;),
    /// без меню, шапки, подвала и скриптов. Пункты списков сохраняются пунктами - в
    /// патчноутах именно в них всё важное.
    /// </summary>
    private static string ArticleText(string html)
    {
        var region = Region(html, "article") ?? Region(html, "main") ?? html;
        region = NoiseRegex().Replace(region, " ");
        var sb = new StringBuilder();
        foreach (Match m in BlockRegex().Matches(region))
        {
            var tag = m.Groups[1].Value.ToLowerInvariant();
            var line = Clean(m.Groups[2].Value, 600);
            if (line.Length < 3) continue;
            sb.Append(tag switch
            {
                "li" => "• ",
                "h1" or "h2" or "h3" or "h4" => "## ",
                _ => "",
            }).Append(line).Append('\n');
            if (sb.Length > MaxArticleChars) break;
        }
        var text = sb.ToString().Trim();
        return text.Length <= MaxArticleChars ? text : text[..MaxArticleChars];
    }

    private static string? Region(string html, string tag)
    {
        var start = Regex.Match(html, $"<{tag}\\b", RegexOptions.IgnoreCase);
        if (!start.Success) return null;
        var end = html.LastIndexOf($"</{tag}>", StringComparison.OrdinalIgnoreCase);
        return end > start.Index ? html[start.Index..end] : null;
    }

    [GeneratedRegex("<(script|style|nav|header|footer|aside|form|noscript|svg)\\b.*?</\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NoiseRegex();

    [GeneratedRegex("<(h[1-4]|p|li)\\b[^>]*>(.*?)</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockRegex();

    /* ---------------- Общее ---------------- */

    private static DateTime? Date(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTimeOffset.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d.UtcDateTime
            : null;
    }

    /// <summary>Текст без HTML и лишних пробелов, не длиннее max.</summary>
    private static string Clean(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var text = WebUtility.HtmlDecode(TagRegex().Replace(raw, " "));
        text = SpaceRegex().Replace(text, " ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    private static string? FirstImage(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        var m = ImgRegex().Match(html);
        return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex("<img[^>]+src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex ImgRegex();
}
