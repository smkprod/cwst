using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ClanWarTracker.Application.Games;

/// <summary>
/// Пропуск к картинке-загадке.
///
/// Нужен из-за неустранимого противоречия: картинку грузит тег &lt;img&gt;, заголовок с
/// initData туда не подставить, значит ручка обязана быть открытой. А если в адресе
/// стоит номер уровня, любой запросит сразу третий, увидит почти весь арт и ответит
/// с первой попытки на три очка.
///
/// Поэтому в адресе не уровень, а подписанная пара «игрок и день». Уровень сервер
/// берёт из базы — из того, сколько попыток человек реально потратил. Подделать
/// нельзя, а подсмотреть вперёд не получится: своя же запись и не пустит.
/// </summary>
public static class PuzzleToken
{
    private const int SigBytes = 10;
    private const int SigChars = 14;   // base64url от 10 байт, без выравнивания

    public static string Create(int playerId, int day, string secret)
    {
        var body = Body(playerId, day);
        return ToBase64Url(body) + ToBase64Url(Sign(body, secret));
    }

    /// <summary>null — подпись не сошлась или адрес испорчен.</summary>
    public static (int PlayerId, int Day)? Verify(string token, string secret)
    {
        if (token.Length <= SigChars) return null;

        byte[] body, sig;
        try
        {
            body = FromBase64Url(token[..^SigChars]);
            sig = FromBase64Url(token[^SigChars..]);
        }
        catch (FormatException) { return null; }

        if (body.Length != 8 || sig.Length != SigBytes) return null;
        if (!CryptographicOperations.FixedTimeEquals(sig, Sign(body, secret))) return null;

        return (BinaryPrimitives.ReadInt32BigEndian(body),
                BinaryPrimitives.ReadInt32BigEndian(body.AsSpan(4)));
    }

    private static byte[] Body(int playerId, int day)
    {
        var body = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(body, playerId);
        BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(4), day);
        return body;
    }

    private static byte[] Sign(byte[] body, string secret) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body).AsSpan(0, SigBytes).ToArray();

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight((b64.Length + 3) / 4 * 4, '='));
    }
}
