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

            string? text = null;
            PlanReminderStage stage;

            if (daysLeft is > 3 and <= 7 && clan.PlanReminderStageSent < PlanReminderStage.SevenDays)
            {
                stage = PlanReminderStage.SevenDays;
                text = $"⏳ Pro-тариф клана заканчивается примерно через 7 дней " +
                       $"({clan.PlanExpiresAtUtc.Value:dd.MM.yyyy} UTC).\n\n" +
                       "Без Pro будут недоступны:\n" +
                       "• Безлимитные личные напоминания (Free — только 5 игроков)\n" +
                       "• Прогноз клана и игроков\n" +
                       "• История войн и DNA-профили\n" +
                       "• Кнопка «Пнуть всех» без ограничений\n\n" +
                       "Свяжитесь с администратором сервиса для продления.";
            }
            else if (daysLeft is > 0 and <= 3 && clan.PlanReminderStageSent < PlanReminderStage.ThreeDays)
            {
                stage = PlanReminderStage.ThreeDays;
                text = $"⚠️ Pro-тариф клана заканчивается через ~3 дня " +
                       $"({clan.PlanExpiresAtUtc.Value:dd.MM.yyyy} UTC).\n\n" +
                       "Поспешите продлить, чтобы не потерять прогнозы, историю и безлимитные напоминания!";
            }
            else if (daysLeft <= 0 && clan.PlanReminderStageSent < PlanReminderStage.Expired)
            {
                stage = PlanReminderStage.Expired;
                text = "🔒 Pro-тариф клана истёк — клан переведён на Free.\n\n" +
                       "Напоминания теперь ограничены 5 привязанными игроками; " +
                       "прогнозы, история и DNA-профили недоступны.\n\n" +
                       "Для продления обратитесь к администратору сервиса.";
            }
            else continue;

            try
            {
                await notifier.SendToChatAsync(clan.TelegramChatId, text, ct);
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
