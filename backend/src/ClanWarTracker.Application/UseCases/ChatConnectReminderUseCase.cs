using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Лидеру клана, где бот не подключён к чату: короткое сообщение в личку с кнопкой
/// «Добавить бота в чат клана» (ссылка ?startgroup - бот подключится сам). Без чата клан
/// не видит итоги дня, напоминания и поздравления - а это главный повод открывать бота.
///
/// Не рассылка: пишем только лидерам и соруководителям, которые сами привязали аккаунт
/// и не закрыли личку; не чаще раза в неделю и не больше трёх раз за всё время - дальше
/// молчим, человек решил.
/// </summary>
public class ChatConnectReminderUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IClashRoyaleApi crApi,
    ISentNotificationRepository sentLog,
    INotificationSender notifier)
{
    private const string WeeklyKind = "chatconnect";
    private const string TotalKind = SentNotification.OncePrefix + "chatconnect";
    public const int MaxTimes = 3;
    private const int TzOffsetMinutes = 180;

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        // Днём по Киеву: в личку ночью не пишем
        var hour = DateTime.UtcNow.AddMinutes(TzOffsetMinutes).Hour;
        if (hour is < 11 or >= 20) return 0;

        var withoutChat = (await clans.GetAllAsync(ct)).Where(c => c.TelegramChatId == 0).ToList();
        if (withoutChat.Count == 0) return 0;

        var thisWeek = await sentLog.GetKeysAsync(WeeklyKind, DateTime.UtcNow.AddDays(-7), ct);
        var total = await sentLog.GetKeysAsync(TotalKind, DateTime.MinValue, ct);
        var sent = 0;

        foreach (var clan in withoutChat)
        {
            var linked = (await players.GetByClanIdAsync(clan.Id, ct)).Where(p => p.TelegramUserId is not null).ToList();
            if (linked.Count == 0) continue;

            Dictionary<string, string> roles;
            try { roles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
            catch { continue; }

            foreach (var p in linked)
            {
                if (roles.GetValueOrDefault(p.PlayerTag) is not ("leader" or "coLeader")) continue;
                var tg = p.TelegramUserId!.Value;
                var key = tg.ToString();
                if (thisWeek.Contains(key)) continue;
                var times = total.Count(k => k.StartsWith(key + "|", StringComparison.Ordinal));
                if (times >= MaxTimes) continue;

                var prefs = await alertPrefs.GetAsync(tg, ct);
                if (prefs is { DmBlocked: true }) continue;

                // Отметки - до отправки: сбой посередине не должен прислать второе
                await sentLog.AddAsync(WeeklyKind, key, ct);
                await sentLog.AddAsync(TotalKind, $"{key}|{times + 1}", ct);
                thisWeek.Add(key);
                total.Add($"{key}|{times + 1}");

                var t = !string.IsNullOrEmpty(prefs?.Lang)
                    ? BotText.For(prefs.Lang)
                    : NotificationSettings.Parse(clan.NotificationSettingsJson).Text;
                var result = await notifier.SendDmAsync(tg, string.Format(t.ChatConnectDm, clan.Name),
                    [[new BotButton(t.ChatConnectButton, Url: ConnectUrl(clan.ClanTag))]], silent: false, ct);
                if (result.Delivered) sent++;
                else if (result.Blocked && prefs is not null)
                {
                    prefs.DmBlocked = true;
                    await alertPrefs.SaveChangesAsync(ct);
                }
            }
        }
        return sent;
    }

    /// <summary>
    /// Кнопка «добавить бота в группу»: startgroup:setup_ТЕГ. Отправщик превращает её в
    /// https://t.me/бот?startgroup=setup_ТЕГ - Telegram покажет выбор чата, а бот, получив
    /// там /start setup_ТЕГ, подключит клан сам.
    /// </summary>
    public static string ConnectUrl(string clanTag) => "startgroup:setup_" + clanTag.TrimStart('#').ToUpperInvariant();
}
