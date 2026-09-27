namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Рекламная кампания: код для ссылки и понятное владельцу имя.
///
/// Код уходит в ссылку /start ad_&lt;код&gt;, по нему и считается воронка. Имя —
/// только для панели, чтобы через месяц было ясно, что такое «ua2».
/// </summary>
public class Campaign
{
    public int Id { get; set; }

    /// <summary>Латиница, цифры, «_» и «-», до 32 символов — то, что Telegram пропускает в start.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Годится ли код для ссылки. Telegram принимает в параметре start только
    /// A-Z, a-z, 0-9, «_» и «-» и не длиннее 64 символов; «ad_» съедает три.
    /// </summary>
    public static bool IsValidCode(string? code) =>
        code is { Length: >= 1 and <= 32 } && code.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-');
}
