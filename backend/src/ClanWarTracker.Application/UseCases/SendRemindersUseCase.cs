using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
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

    /// <param name="chatPostedKeys">
    /// Дедуп сводки в чат: "clanId:season:section:period". Без него сводка уходила бы
    /// каждые 30 минут всё окно напоминания (до 6 одинаковых сообщений подряд).
    /// </param>
    public async Task ExecuteAsync(ISet<string> chatPostedKeys, CancellationToken ct = default)
    {
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API прилёг — не роняем цикл для остальных кланов
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
            // Тег в чате работает по @username; для ЛС ниже отдельно нужен TelegramUserId
            var allLinked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null || !string.IsNullOrEmpty(p.TelegramUsername))
                .OrderBy(p => p.Id)
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Персональные DM — только Pro и если канал включает ЛС
            // В ЛС Telegram не даёт писать первым — нужен подтверждённый TelegramUserId
            var allowedForDm = isPro && settings.Reminders.Channel.WantsDm()
                ? allLinked.Where(kv => kv.Value.TelegramUserId is not null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Domain.Entities.Player>(StringComparer.OrdinalIgnoreCase);

            // Только текущий состав: в списке войны CR API держит и ушедших (за неделю >50).
            Dictionary<string, string> members;
            try { members = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
            catch { members = new(StringComparer.OrdinalIgnoreCase); } // фолбэк — топ-50 в WarRoster
            var roster = WarRoster.CurrentMemberTags(war, members);
            var slackers = war.Participants
                .Where(p => p.DecksUsedToday < 4 && roster.Contains(p.PlayerTag))
                .ToList();

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

            // Тегаем только тех, кого реально можно тегнуть (привязан Telegram). Непривязанных
            // не перечисляем — их имя из CR не пингует и превращает сводку в стену имён.
            var taggable = slackers.Where(s => allLinked.ContainsKey(s.PlayerTag)).ToList();
            if (taggable.Count > 0)
            {
                var names = string.Join("\n", taggable.Select(s =>
                {
                    var p = allLinked[s.PlayerTag];
                    return $"• {TelegramMention.Mention(s.Name, p.TelegramUserId, p.TelegramUsername)} " +
                           $"— осталось {4 - s.DecksUsedToday}/4 🃏";
                }));
                var unlinked = slackers.Count - taggable.Count;
                var note = unlinked > 0 ? $"\n\n👥 Ещё <b>{unlinked}</b> без Telegram — пусть привяжут аккаунт в боте." : "";
                parts.Add($"⏰ <b>Ещё не доиграли войну:</b>\n\n{names}{note}");
            }

            // Up-sell — только ВМЕСТЕ со сводкой по лентяям. Отдельно не шлём: когда все
            // отыграли, голая реклама Pro в чате — это спам.
            if (!isPro && allLinked.Count > 0 && parts.Count > 0)
                parts.Add("🔒 Личные напоминания в DM — функция Pro. Подключи Pro, чтобы никто не забывал про атаки.");

            // Сводка в чат — один раз за военный день (ключ дня), а не каждый тик окна.
            var chatKey = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}";
            if (parts.Count > 0 && clan.TelegramChatId != 0 && settings.Reminders.Channel.WantsChat()
                && chatPostedKeys.Add(chatKey))
            {
                try
                {
                    await notifier.SendToChatAsync(clan.TelegramChatId, string.Join("\n\n", parts),
                        clan.TelegramMessageThreadId, html: true, ct: ct);
                }
                catch
                {
                    chatPostedKeys.Remove(chatKey); // не отправилось — попробуем в следующий тик
                }
            }
        }
    }
}
