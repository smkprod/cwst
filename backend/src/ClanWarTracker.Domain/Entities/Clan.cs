using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class Clan
{
    public int Id { get; set; }
    public required string ClanTag { get; set; }         // #CLAN123
    public required string Name { get; set; }
    public long TelegramChatId { get; set; }             // группа, к которой привязан клан

    /// <summary>
    /// ID темы (Topic) форума в группе, куда слать напоминания/отчёты, если /setup был
    /// выполнен внутри темы. Null — группа без тем или /setup выполнен в общем чате.
    /// </summary>
    public int? TelegramMessageThreadId { get; set; }
    public int ReminderHoursBeforeEnd { get; set; } = 3; // отправлять за X часов до конца дня

    /// <summary>
    /// Гибкие настройки уведомлений в JSON (вкл/выкл и канал ЛС/чат по каждому типу).
    /// Null — используются значения по умолчанию (всё включено, канал «везде»).
    /// Хранится как JSON, чтобы расширять без новых миграций БД.
    /// </summary>
    public string? NotificationSettingsJson { get; set; }

    /// <summary>
    /// Ключ последней недели ("season:section"), для которой уже отправлен анонс начала КВ.
    /// Хранится в БД, чтобы анонс НЕ повторялся при перезапуске воркера (деплое). null — ещё не слали.
    /// </summary>
    public string? LastWarStartKey { get; set; }

    // --- SaaS-тариф ---
    public PlanTier PlanTier { get; set; } = PlanTier.Free;
    public DateTime? PlanExpiresAtUtc { get; set; }      // null = бессрочно

    /// <summary>
    /// Какие стадии напоминания об окончании Pro уже отправлены за текущий оплаченный период.
    /// Сбрасывается в None при каждом продлении Pro.
    /// </summary>
    public PlanReminderStage PlanReminderStageSent { get; set; } = PlanReminderStage.None;

    /// <summary>Когда клан подключили к боту. null — подключён до появления поля.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    public List<Player> Players { get; set; } = [];

    /// <summary>Действующий тариф с учётом срока: просроченный Pro = Free.</summary>
    public PlanTier EffectivePlan(DateTime utcNow) =>
        PlanTier == PlanTier.Pro && (PlanExpiresAtUtc is null || PlanExpiresAtUtc > utcNow)
            ? PlanTier.Pro
            : PlanTier.Free;
}
