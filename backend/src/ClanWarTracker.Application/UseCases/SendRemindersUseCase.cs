using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class SendRemindersUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    /// <summary>Минимальный интервал между напоминаниями одному игроку.</summary>
    private static readonly TimeSpan ReminderCooldown = TimeSpan.FromHours(6);


    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
            if (war is null || !war.IsWarDay) continue;

            var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
            if (!settings.Reminders.Enabled) continue;

            var now = DateTime.UtcNow;
            // Конец дня: время, заданное главой в настройках, иначе допущение (10:00 UTC).
            var dayEnd = settings.NextWarEndUtc(now) ?? war.DayEndsAtUtc;
            var timeLeft = dayEnd - now;

            // Шлём только в окне "X часов до конца"
            if (timeLeft > TimeSpan.FromHours(clan.ReminderHoursBeforeEnd) || timeLeft <= TimeSpan.Zero)
                continue;

            var isPro = clan.EffectivePlan(now) == PlanTier.Pro;

            // Все привязанные игроки клана, отсортированы по Id (стабильный порядок)
            var allLinked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .OrderBy(p => p.Id)
                .ToDictionary(p => p.PlayerTag);

            // Персональные DM — только Pro и если канал включает ЛС
            var allowedForDm = isPro && settings.Reminders.Channel.WantsDm()
                ? allLinked
                : new Dictionary<string, Domain.Entities.Player>(StringComparer.OrdinalIgnoreCase);

            var slackers = war.Participants.Where(p => p.DecksUsedToday < 4).ToList();

            foreach (var slacker in slackers)
            {
                if (!allowedForDm.TryGetValue(slacker.PlayerTag, out var player)) continue;
                if (now - player.LastReminderSentAt < ReminderCooldown) continue; // анти-спам

                var decksLeft = 4 - slacker.DecksUsedToday;
                await notifier.SendToUserAsync(
                    player.TelegramUserId!.Value,
                    $"⚔️ Ты ещё не сыграл Clan War!\n" +
                    $"Осталось колод: {decksLeft}/4\n" +
                    $"До конца дня войны: ~{(int)timeLeft.TotalHours} ч {timeLeft.Minutes} мин", ct);

                player.LastReminderSentAt = now;
            }

            await players.SaveChangesAsync(ct);

            // Сводка в групповой чат: все лентяи, привязанные — кликабельным упоминанием
            // (уведомляет их напрямую, даже без username), плюс up-sell DM для Free.
            var parts = new List<string>();

            if (slackers.Count > 0)
            {
                var names = slackers.Select(s =>
                {
                    var p = allLinked.GetValueOrDefault(s.PlayerTag);
                    return TelegramMention.Mention(s.Name, p?.TelegramUserId, p?.TelegramUsername);
                });
                parts.Add($"⏰ Ещё не доиграли войну: {string.Join(", ", names)}");
            }

            if (!isPro && allLinked.Count > 0)
                parts.Add("🔒 Личные напоминания в DM — функция Pro. Подключи Pro, чтобы никто не забывал про атаки.");

            if (parts.Count > 0 && clan.TelegramChatId != 0 && settings.Reminders.Channel.WantsChat())
                await notifier.SendToChatAsync(clan.TelegramChatId, string.Join("\n\n", parts),
                    clan.TelegramMessageThreadId, html: true, ct: ct);
        }
    }
}
