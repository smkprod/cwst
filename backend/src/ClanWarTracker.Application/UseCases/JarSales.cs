using System.Text.RegularExpressions;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Настройки оплаты через банку Монобанка: выключатель, ссылка на банку и цены в гривнах.
/// Всё задаётся в панели; ключ API Монобанка - только в .env на сервере.
/// </summary>
public static partial class JarSales
{
    public const string OnKey = "jar.on";
    public const string LinkKey = "jar.link";
    public const string Plus7Key = "jar.plus7";
    public const string Plus30Key = "jar.plus30";
    public const string ProKey = "jar.pro";
    /// <summary>id банки в API, найденный по ссылке, и ссылка, для которой он найден.</summary>
    public const string AccountKey = "jar.account";
    public const string AccountForKey = "jar.accountFor";
    /// <summary>Последнее чтение выписки: когда и чем кончилось - для панели.</summary>
    public const string LastPollKey = "jar.lastPoll";

    /// <summary>
    /// Цены по умолчанию - примерно как звёзды по курсу: 59★ и 120★ стоят в Telegram
    /// около 1 и 2 долларов, 350★ - около 6.
    /// </summary>
    public const int DefaultPlus7 = 39;
    public const int DefaultPlus30 = 79;
    public const int DefaultPro = 249;
    public const int MaxUah = 20_000;

    /// <summary>Сколько живёт неоплаченный заказ: столько же бот ищет его код в выписке.</summary>
    public static readonly TimeSpan OrderLife = TimeSpan.FromDays(14);

    /// <param name="On">Оплата через банку включена и есть ссылка - кнопку видно в окне покупки.</param>
    public record Offer(bool On, string? Link, int Plus7, int Plus30, int Pro)
    {
        public int PriceFor(string product, int days) =>
            product == JarOrder.Products.Pro ? Pro
            : days == 7 ? Plus7 : days == 30 ? Plus30 : 0;
    }

    public static async Task<Offer> ReadAsync(IServiceSettingRepository settings, CancellationToken ct)
    {
        var link = NormalizeLink(await settings.GetAsync(LinkKey, ct));
        var on = string.Equals(await settings.GetAsync(OnKey, ct), "on", StringComparison.OrdinalIgnoreCase) && link is not null;
        return new Offer(on, link,
            await IntAsync(settings, Plus7Key, DefaultPlus7, ct),
            await IntAsync(settings, Plus30Key, DefaultPlus30, ct),
            await IntAsync(settings, ProKey, DefaultPro, ct));
    }

    private static async Task<int> IntAsync(IServiceSettingRepository settings, string key, int fallback, CancellationToken ct) =>
        int.TryParse(await settings.GetAsync(key, ct), out var v) ? Math.Clamp(v, 0, MaxUah) : fallback;

    /// <summary>Ссылка только на банку Монобанка: на неё и только на неё ведёт кнопка оплаты.</summary>
    public static string? NormalizeLink(string? raw)
    {
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) s = "https://" + s;
        return Uri.TryCreate(s, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && uri.Host.Equals("send.monobank.ua", StringComparison.OrdinalIgnoreCase)
               && SendIdOf(uri.ToString()) is not null
            ? uri.ToString().TrimEnd('/')
            : null;
    }

    /// <summary>Хвост ссылки send.monobank.ua/jar/XXXX - по нему банка находится в API.</summary>
    public static string? SendIdOf(string? link)
    {
        var m = SendIdRx().Match(link ?? "");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Та же банка: sendId из API бывает и «jar/XXXX», и просто «XXXX».</summary>
    public static bool SameJar(string apiSendId, string linkSendId) =>
        apiSendId.Split('/').Last().Equals(linkSendId, StringComparison.Ordinal);

    /// <summary>Новый код заказа: шесть цифр, первая не ноль.</summary>
    public static string NewCode() => Random.Shared.Next(100_000, 1_000_000).ToString();

    /// <summary>
    /// Коды в комментарии: все отдельные шестизначные числа. Пробелы и дефисы внутри
    /// («482 913», «482-913») прощаем - так набирают с телефона.
    /// </summary>
    public static List<string> CodesIn(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return [];
        var squeezed = SeparatorRx().Replace(comment, "");
        return CodeRx().Matches(squeezed).Select(m => m.Value).Distinct().ToList();
    }

    [GeneratedRegex(@"/jar/([A-Za-z0-9]+)")]
    private static partial Regex SendIdRx();

    [GeneratedRegex(@"(?<=\d)[\s\-–](?=\d)")]
    private static partial Regex SeparatorRx();

    [GeneratedRegex(@"(?<!\d)[1-9]\d{5}(?!\d)")]
    private static partial Regex CodeRx();
}
