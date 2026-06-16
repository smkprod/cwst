namespace ClanWarTracker.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public required string PlayerTag { get; set; }      // #ABC123
    public required string Name { get; set; }
    public long? TelegramUserId { get; set; }            // null = не привязан к Telegram
    public int? ClanId { get; set; }   // null = игрок без клана в боте
    public Clan? Clan { get; set; }
    public DateTime? LastReminderSentAt { get; set; }    // анти-спам напоминаний
}
