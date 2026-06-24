namespace ClanWarTracker.Application.UseCases;

/// <summary>Общие правила валидации полей турнира — используются при создании и редактировании.</summary>
public static class TournamentValidation
{
    /// <summary>
    /// Только ссылки на clashroyale.com — поле рассылается всем участникам и кликается,
    /// произвольный URL здесь превращает турнир в вектор фишинга/спама.
    /// </summary>
    public static bool IsValidClanInviteLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.Length > 300) return false;
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != "https") return false;
        return uri.Host.Equals("clashroyale.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".clashroyale.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);
}
