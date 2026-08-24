using ClanWarTracker.Application.Notifications;
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

        var t = NotificationSettings.Parse(clan.NotificationSettingsJson).Text;

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
        // Кого в этом запуске реально «пнули» (ЛС или тег в чате) — каждому +1 к счётчику
        // пинков, но не больше одного за запуск, даже если достали и туда и туда.
        var nudgedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                string.Format(t.NudgeDm, decksLeft, (int)timeLeft.TotalHours, Math.Max(0, timeLeft.Minutes)), ct);

            player.LastReminderSentAt = now;
            nudgedTags.Add(slacker.PlayerTag);
            dm++;
        }

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
                return string.Format(t.SlackerRow,
                    TelegramMention.Mention(s.Name, p.TelegramUserId, p.TelegramUsername),
                    4 - s.DecksUsedToday);
            }));
            var unlinkedNote = unlinkedCount > 0
                ? "\n\n" + string.Format(t.NudgeUnlinked, unlinkedCount)
                : "";
            try
            {
                await notifier.SendToChatAsync(clan.TelegramChatId,
                    $"{t.NudgeChatTitle}\n\n{names}{unlinkedNote}",
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

        // Счётчик пинков: ЛС + тегнутые в чате (если сводка реально ушла), без двойного счёта
        if (postedToChat)
            foreach (var s in taggable.Take(30)) nudgedTags.Add(s.PlayerTag);
        foreach (var tag in nudgedTags)
            if (linkedPlayers.TryGetValue(tag, out var p)) p.NudgeCount++;
        await players.SaveChangesAsync(ct);

        return new NudgeResult(dm, skipped, taggable.Count, unlinkedCount, postedToChat);
    }
}
