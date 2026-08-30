using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ClanWarTracker.Application.Security;

/// <summary>Что зашито в ссылку-приглашение.</summary>
public record ClaimPayload(int ClanId, string PlayerTag, DateTime ExpiresUtc);

/// <summary>
/// Ссылка-приглашение: лидер отдаёт её человеку, тот жмёт — и привязывается сам.
///
/// Появилась потому, что привязать игрока без @username раньше можно было только
/// ответом на его сообщение в чате. Кто в чате не пишет — а это ровно те, кого чаще
/// всего и приходится пинать, — оставался непривязанным навсегда.
///
/// Код подписан HMAC на токене бота, а не хранится в базе. Так задумано: таблица
/// одноразовых кодов означала бы миграцию и уборку протухших строк ради данных,
/// живущих сутки. Всё нужное лежит внутри самого кода, сервер проверяет подпись
/// и ничего не помнит.
///
/// Одноразовость получается сама собой: код называет тег, а тег после первой
/// привязки уже занят аккаунтом — второй человек по той же ссылке получит отказ
/// (проверка в вызывающем коде). Отдельного счётчика использований не нужно.
///
/// Формат: base64url(полезная часть) + ровно 16 символов base64url подписи.
/// Полезная часть — 4 байта ClanId, 4 байта срока в минутах эпохи, дальше тег ASCII.
/// Всё вместе с префиксом укладывается в 64 символа — предел параметра /start.
/// </summary>
public static class ClaimLink
{
    /// <summary>Префикс параметра /start. По нему бот отличает приглашение от рефералки.</summary>
    public const string Prefix = "claim_";

    /// <summary>Сколько живёт ссылка. Сутки: за это время лидер успевает её передать.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Байт подписи. Двенадцати достаточно: подделка требует перебора 2^96, а каждый
    /// байт — это ещё символы в ссылке, которых у нас всего 64.
    /// </summary>
    private const int SigBytes = 12;

    /// <summary>base64url от 12 байт — ровно 16 символов без выравнивания.</summary>
    private const int SigChars = 16;

    public static string Create(int clanId, string playerTag, DateTime expiresUtc, string secret)
    {
        var body = Body(clanId, playerTag, expiresUtc);
        return Prefix + ToBase64Url(body) + ToBase64Url(Sign(body, secret));
    }

    /// <summary>
    /// Разбирает код. null — подпись не сошлась, срок вышел или код испорчен.
    /// Причину наружу не выдаём: для человека все эти случаи означают одно и то же —
    /// «попроси у лидера новую ссылку», а разбор деталей помогает только подбирающему.
    /// </summary>
    public static ClaimPayload? Verify(string code, string secret, DateTime nowUtc)
    {
        if (code.StartsWith(Prefix, StringComparison.Ordinal)) code = code[Prefix.Length..];
        if (code.Length <= SigChars) return null;

        var bodyPart = code[..^SigChars];
        var sigPart = code[^SigChars..];

        byte[] body, sig;
        try
        {
            body = FromBase64Url(bodyPart);
            sig = FromBase64Url(sigPart);
        }
        catch (FormatException) { return null; }

        // Меньше девяти байт — это не наш код: четыре на клан, четыре на срок и хотя бы
        // один символ тега. Без проверки следующая строка ушла бы за границу массива.
        if (body.Length < 9 || sig.Length != SigBytes) return null;
        if (!CryptographicOperations.FixedTimeEquals(sig, Sign(body, secret))) return null;

        var clanId = BinaryPrimitives.ReadInt32BigEndian(body);
        var minutes = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(4));
        var expires = DateTime.UnixEpoch.AddMinutes(minutes);
        if (expires <= nowUtc) return null;

        var tag = "#" + Encoding.ASCII.GetString(body, 8, body.Length - 8);
        return new ClaimPayload(clanId, tag, expires);
    }

    private static byte[] Body(int clanId, string playerTag, DateTime expiresUtc)
    {
        var tag = Encoding.ASCII.GetBytes(playerTag.TrimStart('#').ToUpperInvariant());
        var body = new byte[8 + tag.Length];
        BinaryPrimitives.WriteInt32BigEndian(body, clanId);
        // Минуты, а не секунды: срок нам нужен с точностью до часа, а четыре лишних
        // бита в секундах — это лишние символы в ссылке.
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(4),
            (uint)(expiresUtc - DateTime.UnixEpoch).TotalMinutes);
        tag.CopyTo(body, 8);
        return body;
    }

    private static byte[] Sign(byte[] body, string secret) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body).AsSpan(0, SigBytes).ToArray();

    /// <summary>
    /// base64url без выравнивания: Telegram принимает в /start только A-Z a-z 0-9 _ -,
    /// а обычный base64 использует «+», «/» и «=».
    /// </summary>
    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight((b64.Length + 3) / 4 * 4, '='));
    }
}
