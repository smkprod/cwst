namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Респект 👏 — лёгкая социальная награда от согильдийца согильдийцу (аналог лайка).
/// Один игрок может дать один респект в сутки (UTC). Копятся навсегда, вечером бот
/// подводит «топ респектов дня» в чате.
/// </summary>
public class Respect
{
    public int Id { get; set; }
    public int ClanId { get; set; }

    public required string FromPlayerTag { get; set; }
    public required string FromName { get; set; }
    public required string ToPlayerTag { get; set; }
    public required string ToName { get; set; }

    /// <summary>Дата (UTC, yyyy-MM-dd) — ключ лимита «1 в сутки».</summary>
    public required string DayUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
