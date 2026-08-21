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

    public record NudgeResult(int NotifiedDm, int SkippedCooldown, int TaggableCount, int UnlinkedCount, bool PostedToChat);

    /// <param name="isPro">Free: рассылка до 5 игроков. Pro: без ограничений.</param>
    /// <returns>null — война не идёт (тренировка) или клан не найден.</returns>
    public async Task<NudgeResult?> ExecuteAsync(int clanId, bool isPro, CancellationToken ct = default)
    {
        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == clanId);
        if (clan is null) return null;

        var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
        if (war is null || !war.IsWarDay) return null;

        var now = DateTime.UtcNow;
        var timeLeft = war.TimeLeft(now);

        // GroupBy (а не ToDictionary): у одного тега может быть несколько записей игрока
        // (перепривязки/дубли) — берём первую, иначе ToDictionary падает на дубль-ключе.
        // Тегнуть можно и того, у кого есть только @username: для упоминания в чате
        // регистрация не нужна. TelegramUserId требуется лишь для личных сообщений.
        var linkedPlayers = (await players.GetByClanIdAsync(clan.Id, ct))
            .Where(p => p.TelegramUserId is not null || !string.IsNullOrEmpty(p.TelegramUsername))
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Только текущий состав клана (список войны CR API держит и ушедших — за неделю >50).
        Dictionary<string, string> members;
        try { members = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
        catch { members = new(StringComparer.OrdinalIgnoreCase); } // фолбэк — топ-50 в WarRoster
        var roster = WarRoster.CurrentMemberTags(war, members);
        var allSlackers = war.Participants
            .Where(p => p.DecksUsedToday < 4 && roster.Contains(p.PlayerTag))
            .ToList();
        // Free: не более 5 человек суммарно получают любые уведомления (Pro — без лимита)
        var slackers = isPro ? allSlackers : allSlackers.Take(5).ToList();

        int dm = 0, skipped = 0;
        foreach (var slacker in slackers)
        {
            if (!linkedPlayers.TryGetValue(slacker.PlayerTag, out var player)) continue;
            if (now - player.LastReminderSentAt < NudgeCooldown) { skipped++; continue; }

            // Telegram запрещает боту писать первым: без ID личное сообщение отправить
            // некуда. Такой игрок всё равно получит тег в чате — это ниже.
            if (player.TelegramUserId is null) continue;

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

        // Публично в чат клана — тегаем ТОЛЬКО тех, кого реально можно тегнуть (привязан
        // Telegram). Непривязанных не пишем: их имя из CR — просто текст, он никого не
        // пингует и раздувает сообщение в стену из 50-100 имён. Внизу — счётчик непривязанных,
        // чтобы глава видел, скольким нужно привязать аккаунт.
        var taggable = slackers.Where(s => linkedPlayers.ContainsKey(s.PlayerTag)).ToList();
        // Счётчик непривязанных — от ПОЛНОГО списка лентяев (allSlackers), а не от урезанного
        // Free-лимитом, иначе на Free цифра «ещё N не привязали» занижается.
        var unlinkedCount = allSlackers.Count(s => !linkedPlayers.ContainsKey(s.PlayerTag));

        var postedToChat = false;
        if (taggable.Count > 0 && clan.TelegramChatId != 0)
        {
            var names = string.Join("\n", taggable.Take(30).Select(s =>
            {
                var p = linkedPlayers[s.PlayerTag];
                return $"• {TelegramMention.Mention(s.Name, p.TelegramUserId, p.TelegramUsername)} " +
                       $"— осталось {4 - s.DecksUsedToday}/4 🃏";
            }));
            var unlinkedNote = unlinkedCount > 0
                ? $"\n\n👥 Ещё <b>{unlinkedCount}</b> без Telegram — их тег не достанет. " +
                  "Админ может привязать их сам: ответь на сообщение игрока командой /bind #ТЕГ"
                : "";
            try
            {
                await notifier.SendToChatAsync(clan.TelegramChatId,
                    $"👊 <b>Админ пнул лентяев!</b>\nНужно срочно отыграть Клановую войну:\n\n{names}{unlinkedNote}",
                    clan.TelegramMessageThreadId, html: true, ct: ct);
                postedToChat = true;
            }
            catch
            {
                // Чат недоступен (бота удалили / чат мигрировал в супергруппу / нет прав) —
                // не роняем всю команду: личные пинки уже разосланы.
                postedToChat = false;
            }
        }

        return new NudgeResult(dm, skipped, taggable.Count, unlinkedCount, postedToChat);
    }
}
