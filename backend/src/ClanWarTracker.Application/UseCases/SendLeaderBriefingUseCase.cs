using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Утренний брифинг лидера» (Pro): в начале каждого военного дня лидеру и соруководителям
/// в ЛС приходит личная сводка-план — вчерашний результат, место в гонке, отрыв от первого
/// и сколько медалей нужно сегодня. Ежедневный ритуал, который делает Pro привычкой.
/// Вызывается каждые 30 минут; окно отправки — первые ~3 часа военного дня, дедуп по дню.
/// </summary>
public class SendLeaderBriefingUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    /// <summary>Окно отправки: день начался, но не старше этого времени.</summary>
    private static readonly TimeSpan SendWindow = TimeSpan.FromHours(3);

    /// <param name="sentKeys">Дедуп между тиками: "clanId:season:section:period".</param>
    /// <returns>Сколько брифингов отправлено (кланов).</returns>
    public async Task<int> ExecuteAsync(ISet<string> sentKeys, CancellationToken ct = default)
    {
        var sent = 0;
        var now = DateTime.UtcNow;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.EffectivePlan(now) != PlanTier.Pro) continue;

            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; }
            if (war is null || !war.IsWarDay) continue;

            // Окно: первые SendWindow часов военного дня (день = 24ч, конец известен).
            var dayStart = war.DayEndsAtUtc.AddHours(-24);
            if (now < dayStart || now - dayStart > SendWindow) continue;

            var key = $"{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}";
            if (sentKeys.Contains(key)) continue;

            // Получатели: привязанные лидер и соруководители из текущего состава CR.
            Dictionary<string, string> roles;
            try { roles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
            catch { continue; }
            var leaderTags = roles
                .Where(kv => kv.Value is "leader" or "coLeader")
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (leaderTags.Count == 0) continue;

            var recipients = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null && leaderTags.Contains(p.PlayerTag))
                .GroupBy(p => p.TelegramUserId!.Value)
                .Select(g => g.First())
                .ToList();
            if (recipients.Count == 0) continue;

            var text = BuildBriefing(war);
            sentKeys.Add(key);
            foreach (var r in recipients)
                await notifier.SendToUserAsync(r.TelegramUserId!.Value, text, ct);
            sent++;
        }

        return sent;
    }

    private static string BuildBriefing(WarStatus war)
    {
        var dayNumber = Math.Clamp(war.PeriodIndex - 2, 1, 4);
        var isColosseum = war.PeriodType == "colosseum";

        // Позиция в гонке по накопленным медалям
        var standings = war.RaceClans.OrderByDescending(c => c.Fame).ToList();
        var ours = standings.FirstOrDefault(c =>
            string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
        var place = ours is null ? 0 : standings.IndexOf(ours) + 1;
        var leader = standings.FirstOrDefault();
        var gap = ours is null || leader is null || ReferenceEquals(ours, leader)
            ? 0
            : leader.Fame - ours.Fame;

        // Вчерашний день из официального лога (последний завершённый военный день)
        var yesterday = war.DayLogs
            .Where(d => d.DayIndex >= 3 && d.DayIndex < war.PeriodIndex)
            .OrderByDescending(d => d.PeriodIndex)
            .FirstOrDefault();

        var lines = new List<string>
        {
            $"🌅 Брифинг лидера — {(isColosseum ? "Колизей" : "война")}, день {dayNumber}/4",
            "",
        };

        if (yesterday is not null)
            lines.Add($"Вчера: {yesterday.PointsEarned:N0} 🏅 · {yesterday.EndOfDayRank}-е место дня");

        if (ours is not null)
        {
            lines.Add($"Сейчас в гонке: {place}-е место · {ours.Fame:N0} 🏅");
            if (gap > 0) lines.Add($"До 1-го места: {gap:N0} 🏅");
            else if (place == 1 && standings.Count > 1)
                lines.Add($"Отрыв от 2-го: {ours.Fame - standings[1].Fame:N0} 🏅");
        }

        var decksLeft = war.Participants.Sum(p => Math.Max(0, 4 - p.DecksUsedToday));
        if (decksLeft > 0)
            lines.Add($"Колод в запасе сегодня: {decksLeft} 🃏");

        lines.Add("");
        lines.Add(gap > 0
            ? "🎯 План: доиграть все колоды и вернуть лидерство. Пни лентяев из Mini App!"
            : "🎯 План: удержать темп — все 4/4 сегодня. Пни лентяев из Mini App!");

        return string.Join("\n", lines);
    }
}
