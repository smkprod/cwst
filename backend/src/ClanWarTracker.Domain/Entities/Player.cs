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
}
