using System.Text.RegularExpressions;

namespace ClanWarTracker.Application.Analytics;

/// <summary>
/// Метка источника в параметре запуска Mini App (startapp). Бот дописывает к разделу
/// суффикс «-c» (пост в чате клана), «-d» (личное сообщение бота), «-b» (ответ на
/// команду), «-k» (канал), ссылки «поделиться» - «-s». Так одна и та же кнопка «Разбор»
/// из чата и из лички считается раздельно, а раздел остаётся прежним. Фронтенд суффикс
/// отрезает для навигации, а сырой параметр отправляет на сервер.
/// </summary>
public static partial class OpenSource
{
    public const string Chat = "chat";
    public const string Dm = "dm";
    public const string Bot = "bot";
    public const string Channel = "channel";
    public const string Share = "share";
    public const string Link = "link";
    public const string Menu = "menu";

    public static readonly string[] All = [Chat, Dm, Bot, Channel, Share, Link, Menu];

    /// <summary>Суффикс по чату: у групп id отрицательный - пост в чате клана, иначе личка.</summary>
    public static string For(long chatId) => chatId < 0 ? "c" : "d";

    /// <summary>Параметр с меткой: раздел (пустой - home) + суффикс источника.</summary>
    public static string Tag(string? param, string suffix) =>
        (string.IsNullOrEmpty(param) ? "home" : param) + "-" + suffix;

    public static (string Source, string Dest) Parse(string? raw)
    {
        raw = raw?.Trim() ?? "";
        if (raw.Length == 0) return (Menu, "home");
        if (raw.Length > 64) raw = raw[..64];

        var source = Link;
        var m = SuffixRegex().Match(raw);
        if (m.Success)
        {
            source = m.Groups[2].Value switch
            {
                "c" => Chat,
                "d" => Dm,
                "b" => Bot,
                "k" => Channel,
                _ => Share,
            };
            raw = m.Groups[1].Value;
        }
        return (source, Dest(raw));
    }

    /// <summary>Раздел без личного хвоста: m_123 → m, ch_CODE → ch, link_TAG → link.</summary>
    private static string Dest(string param)
    {
        if (param.Length == 0) return "home";
        var head = DestRegex().Match(param);
        if (!head.Success) return "other";
        var d = head.Groups[1].Value;
        return d.Length <= 20 ? d : d[..20];
    }

    [GeneratedRegex("^(.*)-([cdbks])$")]
    private static partial Regex SuffixRegex();

    [GeneratedRegex("^((?:m|ch|link)(?=_)|[a-z][a-z_]*)")]
    private static partial Regex DestRegex();
}
