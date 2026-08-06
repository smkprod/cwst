using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Праздничное сообщение в чат клана, когда игрок набирает 900 медалей за военный день
/// (идеальный день: 4 атаки без единого поражения). Позитивное подкрепление на весь чат —
/// герой получает славу, остальные — мотивацию. Фраза выбирается псевдослучайно, но
/// стабильно (по тегу и дню), чтобы при рестарте воркера не менялась.
/// </summary>
public class SendPerfectDayUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IWarSnapshotRepository snapshots,
    INotificationSender notifier)
{
    /// <summary>4 атаки × 225 (все победы) — максимум и «идеальный день».</summary>
    private const int PerfectDayFame = 900;

    private static readonly string[] Phrases =
    [
        "🏆 {0} набил 900 за день! Чемпион. Передай остальным, где брал читы 😎",
        "👑 {0} — 900/900 за день! Противники уже пишут жалобу в Supercell 📝",
        "🚀 {0} набрал 900 медалей за день. NASA интересуется его руками 🚀",
        "💪 {0} сделал идеальный день: 900! Ни одной осечки — машина, а не игрок",
        "⚡ 900 за день от {0}! Оставь немного медалей другим, жадина 😄",
        "🔥 {0} закрыл день на 900! Скамейка запасных в шоке, тренер плачет от счастья",
        "🎯 {0} — 900 из 900! Снайпер. В следующий раз пусть играет с закрытыми глазами",
    ];

    /// <param name="congratulatedKeys">Дедуп между тиками: "clanId:season:section:period:tag".</param>
    /// <returns>Сколько поздравлений отправлено.</returns>
    public async Task<int> ExecuteAsync(ISet<string> congratulatedKeys, CancellationToken ct = default)
    {
        var sent = 0;
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;
            if (!NotificationSettings.Parse(clan.NotificationSettingsJson).PerfectDay.Enabled) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API прилёг — не роняем цикл для остальных кланов
            if (war is null || !war.IsWarDay) continue;

            // Медали за СЕГОДНЯ = недельная (накопительная) слава − финал вчерашнего дня.
            // Суточному счётчику DecksUsedToday не доверяем — он сбрасывается по своим часам
            // (см. SendDailyReportUseCase), а дельта славы врать не умеет.
            var prevDay = war.PeriodIndex > 3
                ? await snapshots.GetSnapshotAsync(clan.Id, war.SeasonId, war.SectionIndex, war.PeriodIndex - 1, ct)
                : null;
            var prevFameByTag = (prevDay?.Players ?? [])
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Fame, StringComparer.OrdinalIgnoreCase);

            // Ушедших из клана не чествуем
            Dictionary<string, string> memberRoles;
            try { memberRoles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
            catch { memberRoles = []; }

            foreach (var p in war.Participants)
            {
                if (memberRoles.Count > 0 && !memberRoles.ContainsKey(p.PlayerTag)) continue;

                var dayFame = p.Fame - prevFameByTag.GetValueOrDefault(p.PlayerTag, 0);
                if (dayFame < PerfectDayFame) continue;

                var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}:{p.PlayerTag}";
                if (congratulatedKeys.Contains(key)) continue;

                var phrase = Phrases[StablePick(key)];
                try
                {
                    await notifier.SendToChatAsync(
                        clan.TelegramChatId, string.Format(phrase, p.Name),
                        clan.TelegramMessageThreadId, ct: ct);
                    congratulatedKeys.Add(key);
                    sent++;
                }
                catch { /* сбой отправки — попробуем в следующий тик */ }
            }
        }
        return sent;
    }

    /// <summary>Стабильный выбор фразы: тот же игрок в тот же день всегда получает одну и ту же.</summary>
    private static int StablePick(string key)
    {
        var hash = 0;
        foreach (var c in key) hash = unchecked(hash * 31 + c);
        return Math.Abs(hash % Phrases.Length);
    }
}
