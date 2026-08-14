using System.Security.Cryptography;
using System.Text;

namespace ClanWarTracker.Api.Auth;

/// <summary>
/// Валидация initData Telegram Mini App по официальной схеме:
/// https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app
///
/// data_check_string собирается из ДЕКОДИРОВАННЫХ значений (Uri.UnescapeDataString,
/// который в отличие от HttpUtility.ParseQueryString не превращает '+' в пробел),
/// пары сортируются по ключу, 'hash' исключается.
/// </summary>
public static class TelegramInitDataValidator
{
    /// <summary>Максимальный возраст initData — сутки (защита от replay).</summary>
    private const long MaxAgeSeconds = 86400;

    public static bool TryValidate(string initData, string botToken, out long telegramUserId) =>
        TryValidate(initData, botToken, out telegramUserId, out _);

    /// <param name="telegramUsername">@username без «@»; null — у пользователя его нет.</param>
    public static bool TryValidate(string initData, string botToken, out long telegramUserId,
        out string? telegramUsername)
    {
        telegramUserId = 0;
        telegramUsername = null;
        if (string.IsNullOrWhiteSpace(initData)) return false;

        // 1. Разбираем query string вручную и декодируем значения ровно один раз
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in initData.Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            pairs[kv[0]] = Uri.UnescapeDataString(kv[1]);
        }

        if (!pairs.TryGetValue("hash", out var incomingHash) || incomingHash.Length == 0)
            return false;

        // 2. data_check_string: все пары кроме hash, сортировка по ключу, join '\n'
        var dataCheckString = string.Join('\n', pairs
            .Where(p => p.Key != "hash")
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}"));

        // 3. HMAC-SHA256: secret = HMAC("WebAppData", botToken), hash = HMAC(secret, dcs)
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var computed = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(incomingHash.ToLowerInvariant())))
            return false;

        // 4. Свежесть
        if (pairs.TryGetValue("auth_date", out var authDateStr) && long.TryParse(authDateStr, out var authDate))
        {
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authDate;
            if (age > MaxAgeSeconds) return false;
        }

        // 5. Хэш сошёлся — достаём id пользователя
        if (!pairs.TryGetValue("user", out var userJson)) return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(userJson);
            telegramUserId = doc.RootElement.GetProperty("id").GetInt64();
            // username есть не у всех аккаунтов Telegram — это нормально
            if (doc.RootElement.TryGetProperty("username", out var uname) &&
                uname.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = uname.GetString();
                if (!string.IsNullOrWhiteSpace(value)) telegramUsername = value;
            }
            return telegramUserId != 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
