using System.Globalization;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Утренний брифинг лидера» (Pro): в начале каждого военного дня лидеру и соруководителям
/// в ЛС приходит личная сводка-план. Не просто цифры, а метрики + что с ними делать:
/// место в гонке и отрыв, темп против прошлой недели с целью «сколько нужно в день»,
/// форма клана за последние недели (спарклайн) и поимённо кто ещё не доиграл (кого пнуть).
/// Ежедневный ритуал, который делает Pro привычкой. Окно — первые ~3 часа дня, дедуп по дню.
/// </summary>
public class SendLeaderBriefingUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier)
{
    /// <summary>Окно отправки: день начался, но не старше этого времени.</summary>
    private static readonly TimeSpan SendWindow = TimeSpan.FromHours(3);

    private const int DecksPerDayPerPlayer = 4;
    private const int MaxClanMembers = 50;

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

            // Форма клана за прошлые недели (для тренда). Не критично — пусто, если API лёг.
            List<RiverRaceLogWeek> log;
            try { log = await crApi.GetRiverRaceLogAsync(clan.ClanTag, ct); }
            catch { log = []; }

            var text = BuildBriefing(war, roles, log, now);
            sentKeys.Add(key);
            foreach (var r in recipients)
            {
                // Per-recipient: сбой у одного лидера не лишает брифинга остальных.
                try { await notifier.SendToUserAsync(r.TelegramUserId!.Value, text, ct); }
                catch { /* сеть/блокировка — пропускаем получателя */ }
            }
            sent++;
        }

        return sent;
    }

    private static string BuildBriefing(
        WarStatus war, IReadOnlyDictionary<string, string> roles, List<RiverRaceLogWeek> log, DateTime now)
    {
        var dayNumber = Math.Clamp(war.PeriodIndex - 2, 1, 4);
        var isColosseum = war.PeriodType == "colosseum";
        var hoursLeft = Math.Max(0, (war.DayEndsAtUtc - now).TotalHours);

        // Позиция в гонке по накопленным медалям недели.
        var standings = war.RaceClans.OrderByDescending(c => c.Fame).ToList();
        var ours = standings.FirstOrDefault(c =>
            string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
        var place = ours is null ? 0 : standings.IndexOf(ours) + 1;
        var currentFame = ours?.Fame ?? 0;

        var lines = new List<string>
        {
            $"🌅 Брифинг лидера · {(isColosseum ? "Колизей" : "Война")} · день {dayNumber}/4",
        };

        // Вчерашний день — только текущей недели (WeekOffset 0), иначе покажем прошлую войну.
        var yesterday = war.DayLogs
            .Where(d => d.WeekOffset == 0 && d.DayIndex >= 3 && d.DayIndex < war.PeriodIndex)
            .OrderByDescending(d => d.DayIndex)
            .FirstOrDefault();
        if (yesterday is not null)
            lines.Add($"Вчера: {Fmt(yesterday.PointsEarned)} 🏅 ({yesterday.EndOfDayRank}-е место дня)");

        // 1) Гонка сейчас + отрыв (кого догонять / от кого отрываться).
        if (ours is not null)
        {
            lines.Add("");
            lines.Add($"📊 Гонка: {place}-е из {standings.Count} · {Fmt(currentFame)} 🏅");
            var leader = standings.FirstOrDefault();
            if (place > 1 && leader is not null)
                lines.Add($"🔴 До 1-го ({leader.Name}): {Fmt(leader.Fame - currentFame)} 🏅");
            else if (place == 1 && standings.Count > 1)
                lines.Add($"🟢 Отрыв от 2-го ({standings[1].Name}): {Fmt(currentFame - standings[1].Fame)} 🏅");
        }

        // 2) Темп против прошлой недели + практичная цель «нужно N/день».
        var lastWeek = log.FirstOrDefault();
        var lastOurs = lastWeek?.Standings.FirstOrDefault(s =>
            string.Equals(s.ClanTag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
        if (lastOurs is { Fame: > 0 })
        {
            var elapsed = Math.Clamp(dayNumber - 1 + (24 - hoursLeft) / 24.0, 0.05, 4);
            var expectedByNow = (int)Math.Round(lastOurs.Fame * elapsed / 4);
            var delta = currentFame - expectedByNow;
            var pct = (double)currentFame / lastOurs.Fame;
            var daysRemaining = Math.Max(0.1, 4 - elapsed);
            var currentPerDay = (int)Math.Round(currentFame / elapsed);

            lines.Add("");
            lines.Add($"⚖️ Против прошлой недели (итог {Fmt(lastOurs.Fame)} 🏅 · {lastOurs.Rank}-е):");
            lines.Add($"{Bar(pct)} {Math.Round(pct * 100)}%");
            lines.Add(delta >= 0
                ? $"📈 Опережаете график на {Fmt(delta)} 🏅"
                : $"📉 Отстаёте от графика на {Fmt(-delta)} 🏅");

            if (currentFame >= lastOurs.Fame)
                lines.Add("🎉 Прошлая неделя уже побита!");
            else
            {
                var needPerDay = (int)Math.Ceiling((lastOurs.Fame - currentFame) / daysRemaining);
                lines.Add($"🎯 Чтобы побить: {Fmt(needPerDay)} 🏅/день (сейчас темп ~{Fmt(currentPerDay)})");
            }
        }

        // 3) Форма клана за последние недели (спарклайн по завершённым войнам).
        var recentWeeks = log
            .Select(w => w.Standings.FirstOrDefault(s =>
                string.Equals(s.ClanTag, war.ClanTag, StringComparison.OrdinalIgnoreCase))?.Fame ?? 0)
            .Where(f => f > 0)
            .Take(6)
            .Reverse()   // от старых к новым
            .ToList();
        if (recentWeeks.Count >= 3)
        {
            lines.Add("");
            var trend = recentWeeks[^1] > recentWeeks[0] ? "растёте 📈"
                : recentWeeks[^1] < recentWeeks[0] ? "проседаете 📉" : "стабильно ➡️";
            lines.Add($"📊 Форма ({recentWeeks.Count} нед.): {Spark(recentWeeks)} {trend}");
            lines.Add($"{K(recentWeeks[0])} → {K(recentWeeks[^1])} за неделю");
        }

        // 4) Кто ещё не доиграл сегодня — поимённо (кого пнуть). Только текущий состав клана.
        var roster = WarRoster.CurrentMemberTags(war, roles);
        var slackers = war.Participants
            .Where(p => p.DecksUsedToday < DecksPerDayPerPlayer && roster.Contains(p.PlayerTag))
            .OrderByDescending(p => DecksPerDayPerPlayer - p.DecksUsedToday)
            .ToList();
        var rosterSize = Math.Min(Math.Max(roster.Count, 1), MaxClanMembers);

        lines.Add("");
        if (slackers.Count == 0)
        {
            lines.Add("✅ Все уже отыграли 4/4 — отличный старт дня!");
        }
        else
        {
            lines.Add($"🎯 Не доиграли: {slackers.Count} из {rosterSize} — пни их:");
            foreach (var s in slackers.Take(6))
                lines.Add($"• {s.Name} — {s.DecksUsedToday}/4");
            if (slackers.Count > 6)
                lines.Add($"…и ещё {slackers.Count - 6}");
            lines.Add("");
            lines.Add("👉 Открой Mini App → кнопка «Пнуть» разошлёт им напоминание.");
        }

        return string.Join("\n", lines);
    }

    /// <summary>Число с пробелами-разделителями: 125550 → «125 550» (без завязки на культуру).</summary>
    private static string Fmt(int n) =>
        n.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ');

    /// <summary>Компактно: 125550 → «126k».</summary>
    private static string K(int n) => n >= 1000 ? $"{Math.Round(n / 1000.0)}k" : n.ToString();

    /// <summary>Полоса прогресса из 10 сегментов ▓/░ по доле 0..1.</summary>
    private static string Bar(double pct)
    {
        var filled = Math.Clamp((int)Math.Round(pct * 10), 0, 10);
        return new string('▓', filled) + new string('░', 10 - filled);
    }

    /// <summary>Мини-график значений блочными символами ▁▂▃▄▅▆▇█.</summary>
    private static string Spark(IReadOnlyList<int> values)
    {
        const string blocks = "▁▂▃▄▅▆▇█";
        if (values.Count == 0) return "";
        var max = Math.Max(1, values.Max());
        return string.Concat(values.Select(v =>
            blocks[Math.Clamp((int)Math.Round((double)v / max * (blocks.Length - 1)), 0, blocks.Length - 1)]));
    }
}
