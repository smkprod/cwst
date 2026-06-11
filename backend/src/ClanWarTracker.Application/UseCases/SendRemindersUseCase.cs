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

            var linkedPlayers = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .ToDictionary(p => p.PlayerTag);

            var slackers = war.Participants.Where(p => p.DecksUsedToday < 4).ToList();

            foreach (var slacker in slackers)
            {
                if (!linkedPlayers.TryGetValue(slacker.PlayerTag, out var player)) continue;
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

            // Сводка в групповой чат, если есть «непривязанные» лентяи
            var unlinked = slackers.Where(s => !linkedPlayers.ContainsKey(s.PlayerTag)).ToList();
            if (unlinked.Count > 0)
            {
                var names = string.Join(", ", unlinked.Select(u => u.Name));
                await notifier.SendToChatAsync(clan.TelegramChatId,
                    $"⏰ Ещё не доиграли войну: {names}", ct);
            }
        }
    }
}
