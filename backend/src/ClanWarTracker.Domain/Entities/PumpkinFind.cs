namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Найденная тыква в хэллоуинской «Охоте на тыквы»: 13 тыкв спрятаны по разделам
/// приложения, каждая находится один раз. Привязано к аккаунту Telegram, а не к
/// клану: охота личная, и её можно продолжить, сменив клан.
/// </summary>
public class PumpkinFind
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }

    /// <summary>Номер тыквы, 1-13.</summary>
    public int PumpkinId { get; set; }

    public DateTime FoundUtc { get; set; }
}
