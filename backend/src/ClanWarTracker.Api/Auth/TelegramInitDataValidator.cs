using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ClanWarTracker.Api.Auth;

/// <summary>
/// Валидация initData из Telegram Mini App.
/// Алгоритм: secret = HMAC_SHA256(key: "WebAppData", data: bot_token);
///           hash   = HMAC_SHA256(key: secret, data: data_check_string).
/// https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app
/// </summary>
public static class TelegramInitDataValidator
{
    public static bool TryValidate(string initData, string botToken, out long telegramUserId)
    {
        telegramUserId = 0;
        var query = HttpUtility.ParseQueryString(initData);
        var hash = query["hash"];
        if (string.IsNullOrEmpty(hash)) return false;

        // data_check_string: все поля кроме hash, отсортированы, через \n
        var pairs = query.AllKeys!
            .Where(k => k is not null && k != "hash")
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => $"{k}={query[k]}");
        var dataCheckString = string.Join('\n', pairs);

        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var computed = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(hash)))
            return false;

        // auth_date не старше 24 часов — защита от повторов
        if (long.TryParse(query["auth_date"], out var authDate))
        {
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authDate;
            if (age > 86400) return false;
        }

        // user = {"id":123, ...}
        var userJson = query["user"];
        if (userJson is null) return false;
        using var doc = System.Text.Json.JsonDocument.Parse(userJson);
        telegramUserId = doc.RootElement.GetProperty("id").GetInt64();
        return true;
    }
}
