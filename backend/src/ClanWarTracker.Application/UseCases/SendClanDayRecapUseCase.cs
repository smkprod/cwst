using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Вечерние «Итоги дня клана» в чат: боец дня, лучшая серия, трёхкоронки, кто больше
/// всех поднял кубков. Война раз в неделю, а ладдер - каждый день: так у чата есть
/// повод для разговора и в дни без КВ, а у игрока - публичное признание.
///
/// Считаем только по боям привязанных (других у нас нет) и только ладдер с Путём
/// легенд. Меньше трёх игроков за день - не пишем: итоги двух человек выглядят
/// как упрёк остальным, а не как праздник.
/// </summary>
public class SendClanDayRecapUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IClashRoyaleApi crApi,
    CollectPlayerBattlesUseCase collect,
    IServiceSettingRepository settings,
    INotificationSender sender)
{
    /// <summary>Рубильник владельца: «off» - итоги не публикуются.</summary>
    public const string KillSwitchKey = "recap.chat";

    /// <summary>
    /// День и час - по Киеву (UTC+3), как и умолчание часового пояса у игроков: у клана
    /// один чат и одно время поста, а большинство наших кланов живёт в этом поясе.
    /// </summary>
    public const int TzOffsetMinutes = TiltMessages.DefaultTzOffsetMinutes;

    /// <summary>21:00 - вечер ещё идёт, но основная игра дня уже сыграна.</summary>
    public const int PostHourLocal = 21;

    public const int MinPlayers = 3;

    /// <param name="sentKeys">Дедуп между проходами и рестартами: "clanId:yyyy-MM-dd".</param>
    /// <returns>Сколько итогов опубликовано.</returns>
    public async Task<int> ExecuteAsync(ISet<string> sentKeys, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var local = now.AddMinutes(TzOffsetMinutes);
        if (local.Hour != PostHourLocal) return 0;
        if (string.Equals(await settings.GetAsync(KillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase))
            return 0;

        var dayStart = TiltMessages.LocalMidnightUtc(now, TzOffsetMinutes);
        var day = local.ToString("yyyy-MM-dd");
        var posted = 0;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;
            var ns = NotificationSettings.Parse(clan.NotificationSettingsJson);
            if (!ns.DayRecap.Enabled) continue;

            var key = $"{clan.Id}:{day}";
            if (sentKeys.Contains(key)) continue;

            try
            {
                var members = await players.GetByClanIdAsync(clan.Id, ct);

                // Ушедших из клана не чествуем. API недоступен - верим своей базе.
                Dictionary<string, string> roles;
                try { roles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
                catch { roles = []; }
                if (roles.Count > 0) members = members.Where(p => roles.ContainsKey(p.PlayerTag)).ToList();

                // Бои копятся только у привязанных - их журналы и дочитываем перед подсчётом:
                // общий сбор раз в три часа, и без этого вечер попал бы в итоги наполовину.
                var linked = members.Where(p => p.TelegramUserId is not null || p.TelegramUsername is not null).ToList();
                foreach (var p in linked)
                {
                    try { await collect.SyncAsync(p.PlayerTag, ct); }
                    catch { /* один недоступный журнал не отменяет итоги */ }
                }

                var tags = linked.Select(p => p.PlayerTag).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var names = linked
                    .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
                List<PlayerBattle> rows = tags.Count == 0
                    ? []
                    : await battles.GetForTagsBetweenAsync(tags, dayStart, now, ct);

                var days = rows
                    .Where(SendMorningDigestUseCase.Counts)
                    .GroupBy(b => b.PlayerTag, StringComparer.OrdinalIgnoreCase)
                    .Select(g => DigestText.Summarize(names.GetValueOrDefault(g.Key) ?? g.Key,
                        g.OrderBy(b => b.BattleTimeUtc).ToList()))
                    .ToList();

                // Решение «писать или нет» принимаем один раз за вечер: иначе каждый проход
                // часа заново дочитывал бы журналы всего клана ради того же ответа.
                if (days.Count < MinPlayers)
                {
                    sentKeys.Add(key);
                    continue;
                }

                var t = ns.Text;
                var text = DigestText.ClanRecap(t, clan.Name, days);
                if (await sender.SendToChatWithButtonsAsync(
                        clan.TelegramChatId, text, DigestText.ClanRecapButtons(t), clan.TelegramMessageThreadId, ct))
                {
                    sentKeys.Add(key);
                    posted++;
                }
            }
            catch { /* сбой одного клана не должен лишать итогов остальные */ }
        }
        return posted;
    }
}
