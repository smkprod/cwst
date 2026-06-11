using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ClanWarTracker.Api.Auth;

public static class TelegramInitDataValidator
{
    public static bool TryValidate(string initData, string botToken, out long telegramUserId)
    {
        telegramUserId = 0;

        if (string.IsNullOrWhiteSpace(initData)) return false;

        // 1. Разбиваем сырую строку на пары Ключ=Значение БЕЗ автоматического декодирования
        var rawPairs = initData.Split('&')
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1]);

        if (!rawPairs.TryGetValue("hash", out var incomingHash)) return false;

        // 2. Собираем data_check_string из оригинальных (закодированных!) значений
        var sortedPairs = rawPairs
            .Where(p => p.Key != "hash")
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}");

        var dataCheckString = string.Join('\n', sortedPairs);

        // 3. Считаем хэш
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var computed = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(incomingHash)))
            return false;

        // 4. Проверяем дату (ее декодировать не нужно, там просто цифры)
        if (rawPairs.TryGetValue("auth_date", out var authDateStr) && long.TryParse(authDateStr, out var authDate))
        {
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authDate;
            if (age > 86400) return false;
        }

        // 5. А вот теперь, когда хэш сошелся, достаем JSON пользователя и ДЕКОДИРУЕМ его для парсинга ID
        if (!rawPairs.TryGetValue("user", out var rawUserJson)) return false;
        
        var decodedUserJson = HttpUtility.UrlDecode(rawUserJson); // Декодируем только тут!

        using var doc = System.Text.Json.JsonDocument.Parse(decodedUserJson);
        telegramUserId = doc.RootElement.GetProperty("id").GetInt64();
        return true;
    }
}