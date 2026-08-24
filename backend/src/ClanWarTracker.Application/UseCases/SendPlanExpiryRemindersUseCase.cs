using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Рассылает напоминания об истечении Pro-тарифа в групповые чаты кланов:
/// за ~7 дней, за ~3 дня, и по факту истечения.
/// Стадийность гарантирует отсутствие повторов; сброс при продлении — в SetClanPlanUseCase.
/// </summary>
public class SendPlanExpiryRemindersUseCase(
    IClanRepository clans,
    INotificationSender notifier)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            // Только Pro-кланы с явным сроком
            if (clan.PlanTier != PlanTier.Pro || clan.PlanExpiresAtUtc is null) continue;
            if (clan.TelegramChatId == 0) continue;

            var daysLeft = (clan.PlanExpiresAtUtc.Value - now).TotalDays;
            var t = NotificationSettings.Parse(clan.NotificationSettingsJson).Text;

            string? text = null;
            PlanReminderStage stage;

            if (daysLeft is > 3 and <= 7 && clan.PlanReminderStageSent < PlanReminderStage.SevenDays)
            {
                stage = PlanReminderStage.SevenDays;
                text = string.Format(t.PlanSevenDays, clan.PlanExpiresAtUtc.Value.ToString("dd.MM.yyyy"));
            }
            else if (daysLeft is > 0 and <= 3 && clan.PlanReminderStageSent < PlanReminderStage.ThreeDays)
            {
                stage = PlanReminderStage.ThreeDays;
                text = string.Format(t.PlanThreeDays, clan.PlanExpiresAtUtc.Value.ToString("dd.MM.yyyy"));
            }
            else if (daysLeft <= 0 && clan.PlanReminderStageSent < PlanReminderStage.Expired)
            {
                stage = PlanReminderStage.Expired;
                text = t.PlanExpired;
            }
            else continue;

            try
            {
                await notifier.SendToChatAsync(clan.TelegramChatId, text, clan.TelegramMessageThreadId, ct: ct);
                clan.PlanReminderStageSent = stage;
            }
            catch
            {
                // Ошибка отправки в чат — не обновляем стадию, попробуем в следующий тик
            }
        }

        await clans.SaveChangesAsync(ct);
    }
}
