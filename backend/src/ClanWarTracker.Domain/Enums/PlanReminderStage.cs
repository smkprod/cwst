namespace ClanWarTracker.Domain.Enums;

/// <summary>
/// Стадия напоминания об окончании Pro-тарифа, уже отправленная за текущий
/// оплаченный период. Значения монотонно возрастают — повторно ту же стадию
/// не шлём; при продлении сбрасывается в None.
/// </summary>
public enum PlanReminderStage
{
    None = 0,
    SevenDays = 1,
    ThreeDays = 2,
    Expired = 3,
}
