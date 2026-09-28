using System.Text;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.Battles;

/// <summary>
/// Тексты «Стоп-тильта»: сигнал, итог захода, «можно» после паузы.
///
/// Сигнал - не длиннее четырёх строк и без цены: он приходит посреди игры, и его
/// читают между боями за пару секунд. Продаёт он сам собой - если пришёл вовремя
/// и с цифрой, которую игрок узнаёт про себя.
/// </summary>
public static class TiltMessages
{
    /// <summary>Смещение по умолчанию, пока приложение не прислало настоящее: Украина летом, UTC+3.</summary>
    public const int DefaultTzOffsetMinutes = 180;

    public static string Alert(
        BotText t, int lossStreak, int? trophiesLost, BattleAnalyzer.TiltProfile profile,
        string? sameCard, int tipIndex, int? freeLeftAfter)
    {
        var sb = new StringBuilder();
        sb.Append(string.Format(t.TiltHead, lossStreak,
            trophiesLost is int lost && lost < 0 ? $" · −{-lost}🏆" : ""));

        sb.Append('\n');
        if (profile.After2Percent is double after2 && after2 < profile.BasePercent)
        {
            sb.Append(string.Format(t.TiltAlertStats, after2.ToString("0"), profile.BasePercent.ToString("0")));
            if (after2 < 45) sb.Append(t.TiltCoin);
        }
        else
        {
            sb.Append(t.TiltAlertGeneric);
        }

        if (!string.IsNullOrEmpty(sameCard)) sb.Append('\n').Append(string.Format(t.TiltSameCard, sameCard));

        var tips = t.TiltTips.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tips.Length > 0) sb.Append('\n').Append(tips[Math.Abs(tipIndex) % tips.Length]);

        if (freeLeftAfter is int left)
            sb.Append("\n\n").Append(left > 0 ? string.Format(t.TiltFreeLeft, left) : t.TiltFreeLast);

        return sb.ToString();
    }

    /// <summary>Кнопки сигнала: пауза отдельной строкой - это главное действие.</summary>
    public static IReadOnlyList<IReadOnlyList<BotButton>> AlertButtons(BotText t, int alertId) =>
    [
        [new BotButton(t.TiltBtnPause, $"tilt|p|{alertId}")],
        [new BotButton(t.TiltBtnGo, $"tilt|g|{alertId}"), new BotButton(t.TiltBtnMute, $"tilt|m|{alertId}")],
    ];

    /// <param name="works">«Пауза работает»: % побед после паузы и без неё; null - данных пока мало.</param>
    public static string Summary(BotText t, TiltAlert a, (double Paused, double NotPaused)? works)
    {
        var sb = new StringBuilder();
        sb.Append(t.TiltSummaryHead).Append('\n');
        sb.Append(string.Format(t.TiltSummaryScore, a.SessionWins, a.SessionLosses,
            a.SessionTrophies == 0 ? "" : $" · {Signed(a.SessionTrophies)}🏆"));
        sb.Append('\n');

        if (a.AfterWins + a.AfterLosses == 0) sb.Append(t.TiltSummaryStopped);
        else if (a.Choice == "pause") sb.Append(string.Format(t.TiltSummaryPause, a.AfterWins, a.AfterLosses));
        else sb.Append(string.Format(t.TiltSummaryAfter, a.AfterWins, a.AfterLosses));

        if (works is { } w)
            sb.Append('\n').Append(string.Format(t.TiltSummaryWorks, w.Paused.ToString("0"), w.NotPaused.ToString("0")));

        return sb.ToString();
    }

    public static string TypeName(BotText t, string? type) => type switch
    {
        "ice" => t.TiltTypeIce,
        "boiling" => t.TiltTypeBoiling,
        "volcano" => t.TiltTypeVolcano,
        _ => "—",
    };

    /// <summary>+34 или −12: минус настоящий, а не дефис, - так цифры читаются ровнее.</summary>
    public static string Signed(int value) => value > 0 ? $"+{value}" : value < 0 ? $"−{-value}" : "0";

    /// <summary>Код языка из настройки клана.</summary>
    public static string Code(BotLang lang) => lang switch
    {
        BotLang.Uk => "uk",
        BotLang.En => "en",
        _ => "ru",
    };

    /// <summary>
    /// На каком языке писать человеку: выбранный в приложении, иначе язык клана,
    /// иначе русский. Раньше без клана всегда был русский - и украинцу бот писал по-русски.
    /// </summary>
    public static async Task<string> LangAsync(
        PlayerAlertPrefs? prefs, Player player, IClanRepository clans, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(prefs?.Lang)) return Code(BotText.ParseLang(prefs.Lang));
        try
        {
            if (player.ClanId is int clanId && await clans.GetByIdAsync(clanId, ct) is { } clan)
                return Code(NotificationSettings.Parse(clan.NotificationSettingsJson).Lang);
        }
        catch { /* язык - не повод промолчать */ }
        return "ru";
    }

    /// <summary>Начало местных суток в UTC.</summary>
    public static DateTime LocalMidnightUtc(DateTime nowUtc, int tzOffsetMinutes)
    {
        var local = nowUtc.AddMinutes(tzOffsetMinutes);
        return DateTime.SpecifyKind(local.Date.AddMinutes(-tzOffsetMinutes), DateTimeKind.Utc);
    }

    /// <summary>Ночь по местному времени: 23:00–08:00.</summary>
    public static bool IsQuietHour(DateTime nowUtc, int tzOffsetMinutes)
    {
        var hour = nowUtc.AddMinutes(tzOffsetMinutes).Hour;
        return hour >= 23 || hour < 8;
    }
}
