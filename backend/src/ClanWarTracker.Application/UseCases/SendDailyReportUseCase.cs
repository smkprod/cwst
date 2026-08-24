using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Авто-отчёт в чат клана после каждого военного дня (всем кланам, не только Pro —
/// это «пассивный онбординг»: весь клан регулярно видит бота в своём чате).
/// Воркер вызывает каждые 30 минут; день считается закрытым, когда финальный
/// снимок дня перестал быть текущим периодом. Отчёт шлём один раз — окно 2 часа
/// после последнего обновления снимка + дедуп по ключу (переживает почти всё,
/// кроме рестарта воркера прямо внутри окна).
/// </summary>
public class SendDailyReportUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    INotificationSender notifier)
{
    /// <summary>Не репостим день, чей финальный снимок старше этого окна.</summary>
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(2);

    /// <param name="reportedKeys">Дедуп между тиками: "clanId:season:section:period".</param>
    /// <returns>Сколько отчётов отправлено.</returns>
    public async Task<int> ExecuteAsync(ISet<string> reportedKeys, CancellationToken ct = default)
    {
        var sent = 0;
        var now = DateTime.UtcNow;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.TelegramChatId == 0) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API лежит — попробуем в следующем тике

            if (war is null) continue;

            var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
            if (!settings.DailyReport.Enabled) continue;

            // Последняя неделя с данными; финальный снимок последнего военного дня
            var lastWeek = await snapshots.GetByClanAsync(clan.Id, weeks: 1, ct);
            var final = lastWeek
                .OrderByDescending(s => s.SeasonId)
                .ThenByDescending(s => s.SectionIndex)
                .ThenByDescending(s => s.PeriodIndex)
                .FirstOrDefault();
            if (final is null) continue;

            // День ещё идёт? (снимок совпадает с текущим периодом войны)
            var isCurrentPeriod = war.IsWarDay &&
                final.SeasonId == war.SeasonId &&
                final.SectionIndex == war.SectionIndex &&
                final.PeriodIndex == war.PeriodIndex;
            if (isCurrentPeriod) continue;

            // Старые новости не постим (день закончился давно)
            if (now - final.CapturedAtUtc > FreshnessWindow) continue;

            var key = $"{clan.Id}:{final.SeasonId}:{final.SectionIndex}:{final.PeriodIndex}";
            if (!reportedKeys.Add(key)) continue;

            try
            {
                var text = await BuildReportAsync(clan, final, settings.Text, ct);
                if (text is null) continue;   // нечем посчитать день — молчим
                await notifier.SendToChatWithAppButtonAsync(
                    clan.TelegramChatId, text, clan.TelegramMessageThreadId, html: true, ct: ct);
                sent++;
            }
            catch
            {
                reportedKeys.Remove(key); // сбой отправки — попробуем в следующий тик (окно 2ч)
            }
        }
        return sent;
    }

    /// <returns>Текст отчёта или null, если день посчитать нечем (нет вчерашнего снимка).</returns>
    private async Task<string?> BuildReportAsync(Clan clan, WarSnapshot final, BotText t, CancellationToken ct)
    {
        var dayNumber = final.PeriodIndex - 2;          // 3..6 -> 1..4
        var isWeekFinal = final.PeriodIndex >= 6;
        var isColosseum = final.PeriodType == "colosseum";

        // Финал недели — отдельный праздничный итог с MVP (по накопленной за неделю славе).
        if (isWeekFinal) return BuildWeeklyRecap(final, isColosseum, t);

        // Слава и колоды за день считаем дельтой накопительных полей относительно
        // снимка предыдущего дня — а не берём DecksUsedToday "как есть". У CR API
        // суточный счётчик сбрасывается по своим часам, не синхронизированным с
        // переходом periodIndex: на стыке дней игрок, который только отыграл и уже
        // получил медали, может на мгновение показать DecksUsedToday=0.
        //
        // Без вчерашнего снимка дельту посчитать нельзя, а подстановка нуля превращает
        // недельную славу в «за день» и раздувает весь отчёт (см. SendPerfectDayUseCase).
        // Лучше не прислать отчёт, чем прислать с выдуманными числами.
        var prevDay = final.PeriodIndex > 3
            ? await snapshots.GetSnapshotAsync(clan.Id, final.SeasonId, final.SectionIndex,
                final.PeriodIndex - 1, ct)
            : null;
        if (final.PeriodIndex > 3 && prevDay is null) return null;
        // GroupBy: у одного тега может быть несколько записей — берём первую (защита от дубль-ключа).
        var prevFameByTag = (prevDay?.Players ?? [])
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Fame, StringComparer.OrdinalIgnoreCase);
        var prevDecksByTag = (prevDay?.Players ?? [])
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DecksUsed, StringComparer.OrdinalIgnoreCase);

        var dayResults = final.Players
            // Игроков, которых вчера в клане не было, пропускаем: их вчерашняя слава
            // неизвестна, а ноль вместо неё засчитал бы им всю неделю как один день.
            .Where(p => prevDay is null || prevFameByTag.ContainsKey(p.PlayerTag))
            .Select(p => (p.PlayerTag, p.Name,
                // Первый военный день (нет вчерашнего снимка): дельта недельного DecksUsed
                // включала бы тренировочные бои — берём суточный счётчик из снимка.
                DecksToday: prevDay is null
                    ? Math.Clamp(p.DecksUsedToday, 0, 4)
                    : Math.Clamp(p.DecksUsed - prevDecksByTag.GetValueOrDefault(p.PlayerTag, 0), 0, 4),
                DayFame: p.Fame - prevFameByTag.GetValueOrDefault(p.PlayerTag, 0)))
            .ToList();

        var dayFame = dayResults.Sum(r => r.DayFame);
        var top = dayResults.Where(r => r.DayFame > 0).OrderByDescending(r => r.DayFame).Take(3).ToList();

        // Лентяи — только из текущего состава клана: снимок недели содержит и ушедших
        // (их медали в итогах дня остаются — они честно заработаны для клана).
        Dictionary<string, string> memberRoles;
        try { memberRoles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
        catch { memberRoles = new(StringComparer.OrdinalIgnoreCase); } // пустой = показываем всех из снимка
        var slackers = dayResults
            .Where(r => r.DecksToday < 4 &&
                        (memberRoles.Count == 0 || memberRoles.ContainsKey(r.PlayerTag)))
            .OrderByDescending(r => 4 - r.DecksToday)
            .Take(50)
            .ToList();

        // Привязанных к Telegram игроков клана — чтобы упомянуть лентяев напрямую в чате.
        var linked = (await players.GetByClanIdAsync(clan.Id, ct))
            .Where(p => p.TelegramUserId is not null || !string.IsNullOrEmpty(p.TelegramUsername))
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var medals = new[] { "🥇", "🥈", "🥉" };
        var lines = new List<string>
        {
            string.Format(t.DayDone, dayNumber),
            string.Format(t.DayMedals, dayFame.ToString("N0")),
        };

        if (top.Count > 0)
        {
            lines.Add("");
            lines.Add(t.TopOfDay);
            lines.AddRange(top.Select((r, i) =>
                $"{medals[i]} {TelegramMention.Escape(r.Name)} — {r.DayFame:N0}"));
        }

        if (slackers.Count > 0)
        {
            lines.Add("");
            lines.Add(t.NotFinishedTitle);
            lines.AddRange(slackers.Take(15).Select(s =>
            {
                var p = linked.GetValueOrDefault(s.PlayerTag);
                return string.Format(t.DaySlackerRow,
                    TelegramMention.Mention(s.Name, p?.TelegramUserId, p?.TelegramUsername), s.DecksToday);
            }));
            if (slackers.Count > 15) lines.Add(string.Format(t.AndMore, slackers.Count - 15));
        }
        else
        {
            lines.Add("");
            lines.Add(t.PerfectDayAll);
        }

        lines.Add("");
        lines.Add(t.FooterDay);

        return string.Join("\n", lines);
    }

    /// <summary>Итог недели: MVP и топ-3 по накопленной за неделю славе (final.Players.Fame —
    /// уже суммарная слава игрока за неделю).</summary>
    private static string BuildWeeklyRecap(WarSnapshot final, bool isColosseum, BotText t)
    {
        var ranked = final.Players
            .Where(p => p.Fame > 0)
            .OrderByDescending(p => p.Fame)
            .ToList();
        var totalFame = final.Players.Sum(p => p.Fame);
        var played = final.Players.Count(p => p.Fame > 0);

        var lines = new List<string>
        {
            isColosseum ? t.WeekDoneColosseum : t.WeekDoneWar,
            string.Format(t.WeekMedals, totalFame.ToString("N0"), played),
        };

        if (ranked.Count > 0)
        {
            lines.Add("");
            lines.Add(string.Format(t.WeekMvp,
                TelegramMention.Escape(ranked[0].Name), ranked[0].Fame.ToString("N0")));

            if (ranked.Count > 1)
            {
                var medals = new[] { "🥇", "🥈", "🥉" };
                lines.Add("");
                lines.Add(t.TopOfWeek);
                lines.AddRange(ranked.Take(3).Select((p, i) =>
                    $"{medals[i]} {TelegramMention.Escape(p.Name)} — {p.Fame:N0}"));
            }
        }

        lines.Add("");
        lines.Add(t.FooterWeek);

        return string.Join("\n", lines);
    }
}
