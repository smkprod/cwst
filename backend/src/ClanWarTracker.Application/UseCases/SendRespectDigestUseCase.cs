using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Вечерний «топ респектов дня» в чат клана: кто сколько 👏 собрал за сутки.
/// Замыкает социальную петлю — респект виден не только получателю, но всему клану
/// (публичное признание работает сильнее приватного).
/// </summary>
public class SendRespectDigestUseCase(
    IClanRepository clans,
    IRespectRepository respects,
    INotificationSender notifier)
{
    /// <summary>Час UTC, в который подводим итог дня (после конца военного дня по умолчанию).</summary>
    private const int DigestHourUtc = 18;

    /// <param name="sentKeys">Дедуп между тиками: "clanId:yyyy-MM-dd".</param>
    public async Task<int> ExecuteAsync(ISet<string> sentKeys, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (now.Hour != DigestHourUtc) return 0;

        var day = now.ToString("yyyy-MM-dd");
        var sent = 0;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;
            if (!NotificationSettings.Parse(clan.NotificationSettingsJson).PerfectDay.Enabled) continue;

            var key = $"{clan.Id}:{day}";
            if (sentKeys.Contains(key)) continue;

            var today = await respects.GetByClanAndDayAsync(clan.Id, day, ct);
            if (today.Count < 2) { sentKeys.Add(key); continue; } // один респект — не повод для дайджеста

            var top = today
                .GroupBy(r => r.ToPlayerTag, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Name: g.First().ToName, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .Take(3)
                .ToList();

            var medals = new[] { "🥇", "🥈", "🥉" };
            var lines = top.Select((x, i) => $"{medals[i]} {x.Name} — {x.Count} 👏");
            var text = $"👏 <b>Респекты дня</b>\n{string.Join("\n", lines)}\n\n" +
                       $"<i>Всего за сегодня: {today.Count}. Респект можно дать раз в день — загляни в приложение.</i>";

            try
            {
                await notifier.SendToChatWithAppButtonAsync(
                    clan.TelegramChatId, text, clan.TelegramMessageThreadId, html: true, ct: ct);
                sentKeys.Add(key);
                sent++;
            }
            catch { /* сбой отправки — попробуем в следующий тик того же часа */ }
        }
        return sent;
    }
}
