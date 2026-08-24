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


    /// <param name="congratulatedKeys">Дедуп между тиками: "clanId:season:section:period:tag".</param>
    /// <returns>Сколько поздравлений отправлено.</returns>
    public async Task<int> ExecuteAsync(ISet<string> congratulatedKeys, CancellationToken ct = default)
    {
        var sent = 0;
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;
            var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
            if (!settings.PerfectDay.Enabled) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API прилёг — не роняем цикл для остальных кланов
            if (war is null || !war.IsWarDay) continue;

            // Медали за СЕГОДНЯ = недельная (накопительная) слава − финал вчерашнего дня.
            // Суточному счётчику DecksUsedToday не доверяем — он сбрасывается по своим часам
            // (см. SendDailyReportUseCase), а дельта славы врать не умеет.
            //
            // КРИТИЧНО: без вчерашнего снимка дельту посчитать НЕЛЬЗЯ. Раньше отсутствующая
            // база молча подставлялась нулём, и вся недельная слава засчитывалась как
            // «за сегодня» — на второй день КВ игрок с 1450 за неделю получал поздравление
            // за 900, набрав 650. Теперь в таком случае молчим: не поздравить обидно,
            // а поздравить ложно — стыдно на весь чат.
            var isFirstWarDay = war.PeriodIndex <= 3;
            var prevDay = isFirstWarDay
                ? null
                : await snapshots.GetSnapshotAsync(clan.Id, war.SeasonId, war.SectionIndex, war.PeriodIndex - 1, ct);
            if (!isFirstWarDay && prevDay is null) continue;   // базы нет — пропускаем клан

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

                // В первый военный день базы и не должно быть: накопленное за неделю = за день.
                // В остальные дни игрока обязано быть во вчерашнем снимке — иначе он вступил
                // среди недели, его вчерашняя слава неизвестна, и ноль подставлять нельзя
                // (см. комментарий выше).
                int prevFame;
                if (isFirstWarDay) prevFame = 0;
                else if (!prevFameByTag.TryGetValue(p.PlayerTag, out prevFame)) continue;

                // Ровно максимум: меньше — не идеальный день, а больше за день физически
                // не набрать, значит база врёт — и это не повод писать в чат.
                if (p.Fame - prevFame != PerfectDayFame) continue;

                var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}:{p.PlayerTag}";
                if (congratulatedKeys.Contains(key)) continue;

                // Шутка выбирается по ключу дня, а не случайно: повторный тик того же
                // дня не должен поздравлять тем же человеком, но другими словами.
                var jokes = settings.Text.PerfectDayJokes;
                var phrase = jokes[StablePick(key) % jokes.Length];
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

    /// <summary>
    /// Стабильный номер фразы: тот же игрок в тот же день всегда получает одну и ту же.
    /// Возвращает неотрицательное число, а остаток по длине берёт вызывающий — наборов
    /// шуток теперь три (по одному на язык), и они могут быть разной длины.
    /// Маска вместо Math.Abs намеренно: Math.Abs(int.MinValue) бросает исключение.
    /// </summary>
    private static int StablePick(string key)
    {
        var hash = 0;
        foreach (var c in key) hash = unchecked(hash * 31 + c);
        return hash & 0x7FFFFFFF;
    }
}
