using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ClanWarTracker.Application.Notifications;

/// <summary>
/// Тексты постов канала и «простая разметка», в которой они хранятся: обычный текст,
/// **жирный** и [ссылка](https://…). Владелец правит черновики в панели - HTML-теги
/// в поле ввода он бы ломал, а эти две пометки понятны без объяснений.
/// Канал русскоязычный, поэтому тексты здесь только по-русски.
/// </summary>
public static partial class ChannelText
{
    /// <summary>Простая разметка → HTML для Telegram. Всё остальное экранируется.</summary>
    public static string ToHtml(string lite)
    {
        var s = lite.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        s = LinkRegex().Replace(s, m => $"<a href=\"{m.Groups[2].Value.Replace("\"", "&quot;")}\">{m.Groups[1].Value}</a>");
        s = BoldRegex().Replace(s, "<b>$1</b>");
        return s.Trim();
    }

    [GeneratedRegex(@"\[([^\]\n]{1,200})\]\((https?://[^\s)]{1,500})\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*([^*\n]{1,300})\*\*")]
    private static partial Regex BoldRegex();

    /// <summary>«**» и «[» в чужом тексте (ник, заголовок ленты) не должны превратиться в разметку.</summary>
    public static string Plain(string? s) =>
        (s ?? "").Replace("**", "*").Replace("[", "(").Replace("]", ")").Trim();

    /// <summary>3912 → «3 912» (узкий неразрывный пробел - как в игре).</summary>
    public static string Num(int n) =>
        n.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");

    public static string Signed(int n) => n > 0 ? "+" + Num(n) : n < 0 ? "−" + Num(-n) : "0";

    /// <summary>1 билет, 2 билета, 5 билетов.</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var a = Math.Abs(n) % 100;
        var b = a % 10;
        if (a is >= 11 and <= 14) return many;
        return b == 1 ? one : b is >= 2 and <= 4 ? few : many;
    }

    private static readonly string[] Months =
        ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];

    /// <summary>«11 октября» - без культуры ru-RU: в контейнере её может не быть.</summary>
    public static string Day(DateTime local) => $"{local.Day} {Months[local.Month - 1]}";

    public static string Medal(int rank) => rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"{rank}." };

    public static string Rarity(string? rarity) => rarity?.ToLowerInvariant() switch
    {
        "common" => "Обычная",
        "rare" => "Редкая",
        "epic" => "Эпическая",
        "legendary" => "Легендарная",
        "champion" => "Чемпион",
        _ => "",
    };

    /// <summary>Правило челленджа одной фразой - для анонса.</summary>
    public static string RuleLine(string rule) => rule switch
    {
        "threecrowns" => "Очко за каждую победу с тремя коронами.",
        "flawless" => "Очко за каждую победу без потери башни.",
        "streak" => "Побеждает лучшая серия побед подряд.",
        "heavy" => "Очко за победу колодой со средним эликсиром больше 7.",
        _ => "Билет за каждую победу в ладдере или Пути легенд, за каждую третью победу подряд - два.",
    };

    public static string Points(int n, string rule) => rule == "tickets"
        ? $"{Num(n)} {Plural(n, "билет", "билета", "билетов")}"
        : rule == "streak"
            ? $"серия {Num(n)}"
            : $"{Num(n)} {Plural(n, "очко", "очка", "очков")}";

    /// <summary>Строки собираются в пост: пустые подряд схлопываются, края обрезаются.</summary>
    public static string Join(IEnumerable<string?> lines)
    {
        var sb = new StringBuilder();
        var blank = true;
        foreach (var raw in lines)
        {
            if (raw is null) continue;
            var line = raw.TrimEnd();
            if (line.Length == 0)
            {
                if (!blank) sb.Append('\n');
                blank = true;
                continue;
            }
            sb.Append(line).Append('\n');
            blank = false;
        }
        return sb.ToString().Trim();
    }
}
