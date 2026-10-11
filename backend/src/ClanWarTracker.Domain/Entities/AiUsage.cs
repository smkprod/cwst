namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Сколько раз игрок спросил нейросеть за день по одной функции (тренер, разбор, колода).
/// Лимит - ради баланса владельца: запрос копеечный, но без потолка один энтузиаст
/// мог бы гонять его сутки напролёт.
/// </summary>
public class AiUsage
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }

    /// <summary>coach, analysis, deck.</summary>
    public required string Feature { get; set; }

    /// <summary>День по Киеву, yyyy-MM-dd.</summary>
    public required string Day { get; set; }

    public int Count { get; set; }
}
