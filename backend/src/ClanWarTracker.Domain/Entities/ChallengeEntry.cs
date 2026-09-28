namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Участник уикенд-челленджа. Билеты не храним: они считаются из журнала боёв, и
/// таблица всегда равна тому, что было в игре, - ни пересчётов, ни споров.
/// </summary>
public class ChallengeEntry
{
    public int Id { get; set; }

    /// <summary>Какое событие: ключ вида «weekend-20261003».</summary>
    public required string EventId { get; set; }

    public long TelegramUserId { get; set; }
    public required string PlayerTag { get; set; }
    public required string Name { get; set; }

    /// <summary>Бои до вступления не считаются: иначе вступивший в воскресенье получил бы субботу даром.</summary>
    public DateTime JoinedUtc { get; set; }
}
