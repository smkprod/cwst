using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class Clan
{
    public int Id { get; set; }
    public required string ClanTag { get; set; }         // #CLAN123
    public required string Name { get; set; }
    public long TelegramChatId { get; set; }             // группа, к которой привязан клан
    public int ReminderHoursBeforeEnd { get; set; } = 3; // отправлять за X часов до конца дня

    // --- SaaS-тариф ---
    public PlanTier PlanTier { get; set; } = PlanTier.Free;
    public DateTime? PlanExpiresAtUtc { get; set; }      // null = бессрочно

    public List<Player> Players { get; set; } = [];

    /// <summary>Действующий тариф с учётом срока: просроченный Pro = Free.</summary>
    public PlanTier EffectivePlan(DateTime utcNow) =>
        PlanTier == PlanTier.Pro && (PlanExpiresAtUtc is null || PlanExpiresAtUtc > utcNow)
            ? PlanTier.Pro
            : PlanTier.Free;
}
