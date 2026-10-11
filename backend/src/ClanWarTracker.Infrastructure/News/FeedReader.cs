using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// RSS 2.0 и Atom: этого хватает и сайтам, и YouTube (у каждого канала есть Atom-лента
/// youtube.com/feeds/videos.xml?channel_id=…), и Reddit (.rss у любого сабреддита).
/// Ленты задаёт только владелец в панели.
/// </summary>
public partial class FeedReader(HttpClient http) : INewsFeedReader
{
    /// <summary>Лента больше пары мегабайт - не лента, а что-то не то по ссылке.</summary>
    private const int MaxBytes = 3_000_000;

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    public async Task<List<NewsItem>> ReadAsync(string feedUrl, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("bad feed url");

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        // Reddit и часть сайтов отвечают 429/403 на запрос без внятного User-Agent
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ClanifyBot/1.0; +https://t.me/clanifybot)");
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        if (resp.Content.Headers.ContentLength > MaxBytes) throw new InvalidOperationException("feed too large");

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes) throw new InvalidOperationException("feed too large");
        }
        buffer.Position = 0;

        // DTD запрещены: внешние сущности в чужом XML - классическая дыра
        using var xml = XmlReader.Create(buffer, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidOperationException("empty feed");

        var items = root.Name == Atom + "feed" ? ParseAtom(root) : ParseRss(root);
        return items
            .Where(i => i.Title.Length > 0 && i.Link.Length > 0)
            .OrderByDescending(i => i.PublishedUtc ?? DateTime.MinValue)
            .Take(30)
            .ToList();
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
            var summary = (string?)group?.Element(Media + "description")
                          ?? (string?)e.Element(Atom + "summary")
                          ?? (string?)e.Element(Atom + "content");
            var image = videoId is { Length: > 0 }
                ? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"
                : (string?)group?.Element(Media + "thumbnail")?.Attribute("url")
                  ?? (string?)e.Element(Media + "thumbnail")?.Attribute("url")
                  ?? FirstImage(summary);
            yield return new NewsItem(
                Key: (string?)e.Element(Atom + "id") ?? href,
                Title: Clean((string?)e.Element(Atom + "title"), 300),
                Summary: Clean(summary, 2000),
                Link: href,
                ImageUrl: image,
                PublishedUtc: Date((string?)e.Element(Atom + "published") ?? (string?)e.Element(Atom + "updated")));
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
