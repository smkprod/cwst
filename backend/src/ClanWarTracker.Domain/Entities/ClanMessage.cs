namespace ClanWarTracker.Domain.Entities;

/// <summary>О чём сообщение: просто слова или вызов на бой.</summary>
public enum ClanMessageKind
{
    /// <summary>Обычное сообщение клану.</summary>
    Message = 0,

    /// <summary>Вызов сыграть — доступен только кланам, где есть спонсор.</summary>
    Challenge = 1,
}

/// <summary>
/// Сообщение от клана клану, отправленное через бота.
///
/// Хранится не ради истории переписки, а ради ограничения: пара кланов может
/// написать друг другу раз в сутки, и проверить это можно только по записям.
/// Без такого ограничения фича превращается в канал рассылки, а рассылка через
/// чужого бота заканчивается жалобой и блокировкой самого бота, а не отправителя.
/// </summary>
public class ClanMessage
{
    public int Id { get; set; }

    public int FromClanId { get; set; }
    public Clan? FromClan { get; set; }

    public int ToClanId { get; set; }
    public Clan? ToClan { get; set; }

    /// <summary>Кто отправил — чтобы в чате было видно не только клан, но и человека.</summary>
    public long SentByTelegramUserId { get; set; }
    public required string SentByName { get; set; }

    public ClanMessageKind Kind { get; set; }

    public required string Text { get; set; }

    public DateTime SentAtUtc { get; set; }
}
