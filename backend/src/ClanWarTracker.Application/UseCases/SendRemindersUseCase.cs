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

            var now = DateTime.UtcNow;
            var timeLeft = war.TimeLeft(now);

            // Шлём только в окне "X часов до конца"
            if (timeLeft > TimeSpan.FromHours(clan.ReminderHoursBeforeEnd) || timeLeft <= TimeSpan.Zero)
                continue;

            var isPro = clan.EffectivePlan(now) == PlanTier.Pro;

            // Все привязанные игроки клана, отсортированы по Id (стабильный порядок)
            var allLinked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .OrderBy(p => p.Id)
                .ToDictionary(p => p.PlayerTag);

            // Персональные DM — только Pro
            var allowedForDm = isPro
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

            // Сводка в групповой чат: непривязанные лентяи + up-sell для Free
            var unlinked = slackers.Where(s => !allLinked.ContainsKey(s.PlayerTag)).ToList();
            var parts = new List<string>();

            if (unlinked.Count > 0)
                parts.Add($"⏰ Ещё не доиграли войну: {string.Join(", ", unlinked.Select(u => u.Name))}");

            if (!isPro && allLinked.Count > 0)
                parts.Add("🔒 Личные напоминания в DM — функция Pro. Подключи Pro, чтобы никто не забывал про атаки.");

            if (parts.Count > 0)
                await notifier.SendToChatAsync(clan.TelegramChatId, string.Join("\n\n", parts), ct);
        }
    }
}
