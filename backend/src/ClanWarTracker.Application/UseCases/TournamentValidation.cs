namespace ClanWarTracker.Application.UseCases;

/// <summary>Общие правила валидации полей турнира — используются при создании и редактировании.</summary>
public static class TournamentValidation
{
    /// <summary>Режим дружеского боя: известный ключ DuelModes или null («любой»).</summary>
    public static string? GameMode(string? key) =>
        string.IsNullOrWhiteSpace(key) || DuelModes.All.All(m => m.Key != key) ? null : key;

    /// <summary>Секрет ссылок на виджеты OBS: 24 символа из букв и цифр, угадать нельзя.</summary>
    public static string NewOverlayKey()
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyz23456789";
        Span<byte> bytes = stackalloc byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var chars = new char[24];
        for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    /// <summary>
    /// Только ссылки на clashroyale.com — поле рассылается всем участникам и кликается,
    /// произвольный URL здесь превращает турнир в вектор фишинга/спама.
    /// </summary>
    /// <summary>
    /// Ссылка не указана — это не ошибка: её добавляют позже. Проверяем только то,
    /// что человек всё-таки ввёл.
    /// </summary>
    public static bool IsMissing(string? link) => string.IsNullOrWhiteSpace(link);

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
