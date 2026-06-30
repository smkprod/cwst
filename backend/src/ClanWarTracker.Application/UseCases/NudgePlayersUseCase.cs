using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Пнуть лентяев» — мгновенная рассылка напоминаний всем, кто не доиграл сегодня.
/// Запускается админом из Mini App. Анти-спам: не чаще раза в 30 минут на игрока.
/// </summary>
public class NudgePlayersUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    private static readonly TimeSpan NudgeCooldown = TimeSpan.FromMinutes(30);

    public record NudgeResult(int NotifiedDm, int SkippedCooldown, int UnlinkedCount, bool PostedToChat);

    /// <param name="isPro">Free: рассылка до 20 игроков. Pro: без ограничений.</param>
    /// <returns>null — война не идёт (тренировка) или клан не найден.</returns>
    public async Task<NudgeResult?> ExecuteAsync(int clanId, bool isPro, CancellationToken ct = default)
    {
        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == clanId);
        if (clan is null) return null;

        var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
        if (war is null || !war.IsWarDay) return null;

        var now = DateTime.UtcNow;
        var timeLeft = war.TimeLeft(now);

        var linkedPlayers = (await players.GetByClanIdAsync(clan.Id, ct))
            .Where(p => p.TelegramUserId is not null)
            .ToDictionary(p => p.PlayerTag);

        var allSlackers = war.Participants.Where(p => p.DecksUsedToday < 4).ToList();
        // Free: не более 20 человек суммарно получают любые уведомления
        var slackers = isPro ? allSlackers : allSlackers.Take(20).ToList();

        int dm = 0, skipped = 0;
        foreach (var slacker in slackers)
        {
            if (!linkedPlayers.TryGetValue(slacker.PlayerTag, out var player)) continue;
            if (now - player.LastReminderSentAt < NudgeCooldown) { skipped++; continue; }

            var decksLeft = 4 - slacker.DecksUsedToday;
            await notifier.SendToUserAsync(
                player.TelegramUserId!.Value,
                $"👊 Пинок тебе под зад го кв\n" +
                $"Осталось колод: {decksLeft}/4\n" +
                $"До конца дня: ~{(int)timeLeft.TotalHours} ч {Math.Max(0, timeLeft.Minutes)} мин", ct);

            player.LastReminderSentAt = now;
            dm++;
        }
        await players.SaveChangesAsync(ct);

        // Публично в чат клана — все лентяи, привязанные помечены кликабельным упоминанием,
        // у каждого указано сколько колод осталось доиграть.
        var postedToChat = false;
        if (slackers.Count > 0 && clan.TelegramChatId != 0)
        {
            var names = string.Join(", ", slackers.Take(20).Select(s =>
            {
                var p = linkedPlayers.GetValueOrDefault(s.PlayerTag);
                return $"{TelegramMention.Mention(s.Name, p?.TelegramUserId, p?.TelegramUsername)} " +
                       $"({4 - s.DecksUsedToday}/4 колод)";
            }));
            await notifier.SendToChatAsync(clan.TelegramChatId,
                $"👊 Админ пнул лентяев! Нужно отыграть КВ — осталось:\n{names}",
                clan.TelegramMessageThreadId, html: true, ct: ct);
            postedToChat = true;
        }

        return new NudgeResult(dm, skipped, postedToChat ? slackers.Count : 0, postedToChat);
    }
}
