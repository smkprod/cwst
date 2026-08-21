namespace ClanWarTracker.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public required string PlayerTag { get; set; }      // #ABC123
    public required string Name { get; set; }
    public long? TelegramUserId { get; set; }            // null = не привязан к Telegram
    public string? TelegramUsername { get; set; }        // @username в Telegram (для тегов в чате), null — нет
    public int? ClanId { get; set; }   // null = игрок без клана в боте
    public Clan? Clan { get; set; }
    public DateTime? LastReminderSentAt { get; set; }    // анти-спам напоминаний
    public DateTime? LastSmartAlertSentAt { get; set; }   // анти-спам персональных алертов о влиянии на победу
    /// <summary>Кто пригласил этого игрока (Telegram ID пригласившего). null — пришёл сам.</summary>
    public long? ReferrerTelegramUserId { get; set; }

    /// <summary>Когда игрок привязал аккаунт. null — привязан до появления поля.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    /// <summary>
    /// Привязку сделал лидер клана через /bind, а не сам игрок. Нужно, чтобы отличать
    /// данные «со слов главы» от подтверждённых, и чтобы собственный /link игрока
    /// такую привязку перебивал — он лучше знает, какой аккаунт его.
    /// </summary>
    public bool LinkedByLeader { get; set; }

    // «Что нового»: снимок на момент прошлого визита в Mini App — для персональной
    // карточки-дельты при входе (ранг ↑/↓, медали +N, респекты с прошлого раза).
    public DateTime? LastVisitAtUtc { get; set; }
    public int? LastVisitFame { get; set; }
    public int? LastVisitRank { get; set; }
}
