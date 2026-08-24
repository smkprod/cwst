using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Дисциплина клана»: кто недоигрывает войну, кого приходится пинать и кто тянет
/// до последнего часа. Отдельная ручка, а не часть /status: расчёт лезет в историю
/// снапшотов и в журнал боёв, а статус опрашивается каждую минуту каждым открытым
/// Mini App — тащить туда тяжёлую аналитику незачем.
/// </summary>
public class GetClanDisciplineUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    IWarBattleRepository warBattles,
    IMemoryCache cache)
{
    /// <summary>Сколько завершённых недель берём в расчёт.</summary>
    private const int WeeksWindow = 8;

    /// <summary>Военных колод за неделю: 4 военных дня × 4 колоды.</summary>
    private const int WarDecksPerWeek = 16;

    /// <summary>Бой в пределах этого времени до конца дня считаем «в последний момент».</summary>
    private const double LastMinuteHours = 1.0;

    /// <summary>Если время конца дня не задано главой — допущение CR по умолчанию.</summary>
    private const int DefaultWarEndMinuteUtc = 10 * 60;

    /// <summary>Сколько строк отдаём в каждом списке.</summary>
    private const int TopRows = 5;

    public async Task<ClanDisciplineDto> ExecuteAsync(Clan clan, CancellationToken ct = default)
    {
        var cacheKey = $"discipline:{clan.Id}";
        if (cache.TryGetValue(cacheKey, out ClanDisciplineDto? cached) && cached is not null)
            return cached;

        // Считаем только по текущему составу: разбирать дисциплину тех, кто уже ушёл,
        // незачем — глава на них всё равно не повлияет.
        Dictionary<string, ClanMemberInfo> members;
        try { members = await crApi.GetClanMembersAsync(clan.ClanTag, ct); }
        catch { members = new(StringComparer.OrdinalIgnoreCase); }

        var weekMaps = await BuildWeekMapsAsync(clan, ct);

        // Имя игрока берём из самой свежей недели, где он встречается: в снапшотах и
        // журнале оно записано на момент недели, а люди переименовываются.
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var week in weekMaps)
            foreach (var (tag, row) in week)
                names.TryAdd(tag, row.Name);

        var linked = (await players.GetByClanIdAsync(clan.Id, ct))
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var battleStats = await BuildBattleStatsAsync(clan, ct);

        // Кого вообще разбираем: текущий состав, а если состав не отдался — все, кто
        // встретился в истории (иначе карточка молча опустела бы при сбое CR API).
        var tags = members.Count > 0
            ? members.Keys.ToList()
            : names.Keys.ToList();

        var rows = new List<DisciplinePlayerDto>();
        foreach (var tag in tags)
        {
            var missedDecks = 0;
            var missedWeeks = 0;
            var weeksTracked = 0;

            foreach (var week in weekMaps)
            {
                // Нет в составе той недели — эта неделя к игроку отношения не имеет.
                // Иначе новичок с первого дня выглядел бы худшим прогульщиком клана.
                if (!week.TryGetValue(tag, out var row)) continue;

                weeksTracked++;
                missedDecks += Math.Max(0, WarDecksPerWeek - row.DecksUsed);
                if (row.DecksUsed == 0) missedWeeks++;
            }

            linked.TryGetValue(tag, out var player);
            battleStats.TryGetValue(tag, out var battles);
            var hasName = names.TryGetValue(tag, out var historicName);

            rows.Add(new DisciplinePlayerDto(
                PlayerTag: tag,
                Name: hasName ? historicName! : player?.Name ?? tag,
                AvatarEmoji: player?.AvatarEmoji,
                MissedDecks: missedDecks,
                MissedWeeks: missedWeeks,
                WeeksTracked: weeksTracked,
                NudgeCount: player?.NudgeCount ?? 0,
                LastMinuteBattles: battles?.LastMinute ?? 0,
                TotalBattles: battles?.Total ?? 0,
                AvgHoursBeforeEnd: battles is { Total: > 0 }
                    ? Math.Round(battles.SumHoursBeforeEnd / battles.Total, 1)
                    : 0));
        }

        var result = new ClanDisciplineDto(
            WeeksAnalyzed: weekMaps.Count,
            Skippers: rows
                .Where(r => r.MissedDecks > 0 && r.WeeksTracked > 0)
                .OrderByDescending(r => r.MissedDecks)
                .ThenByDescending(r => r.MissedWeeks)
                .Take(TopRows)
                .ToList(),
            Nudged: rows
                .Where(r => r.NudgeCount > 0)
                .OrderByDescending(r => r.NudgeCount)
                .Take(TopRows)
                .ToList(),
            // Одиночный поздний бой — случайность, а не привычка: нужен хотя бы второй.
            LastMinute: rows
                .Where(r => r.LastMinuteBattles >= 2)
                .OrderByDescending(r => r.LastMinuteBattles)
                .ThenBy(r => r.AvgHoursBeforeEnd)
                .Take(TopRows)
                .ToList());

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        });
        return result;
    }

    /// <summary>Итог недели по игроку: имя на тот момент и сколько колод отыграл.</summary>
    private record WeekRow(string Name, int DecksUsed);

    /// <summary>
    /// Завершённые недели, новые первыми. Собственные снапшоты точнее (в них колоды уже
    /// очищены от тренировочных боёв), поэтому неделя из журнала берётся только тогда,
    /// когда своего снимка за неё нет — например, до подключения клана к боту.
    /// </summary>
    private async Task<List<Dictionary<string, WeekRow>>> BuildWeekMapsAsync(Clan clan, CancellationToken ct)
    {
        var byWeek = new Dictionary<(int Season, int Section), Dictionary<string, WeekRow>>();

        var recent = await snapshots.GetByClanAsync(clan.Id, WeeksWindow + 2, ct);
        foreach (var week in recent.GroupBy(s => (s.SeasonId, s.SectionIndex)))
        {
            // Финал недели — снимок с наибольшим PeriodIndex
            var final = week.OrderByDescending(s => s.PeriodIndex).First();
            byWeek[week.Key] = final.Players
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new WeekRow(g.First().Name, g.First().DecksUsed),
                    StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            foreach (var w in await crApi.GetRiverRaceLogAsync(clan.ClanTag, ct))
            {
                if (byWeek.ContainsKey((w.SeasonId, w.SectionIndex))) continue;
                var ours = w.Standings.FirstOrDefault(s =>
                    string.Equals(s.ClanTag, clan.ClanTag, StringComparison.OrdinalIgnoreCase));
                if (ours is null) continue;

                byWeek[(w.SeasonId, w.SectionIndex)] = ours.Participants
                    .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => new WeekRow(g.First().Name, g.First().DecksUsed),
                        StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { /* журнал не критичен: посчитаем по своим снимкам */ }

        // Текущую неделю не берём — она ещё не доиграна, и любой попал бы в прогульщики.
        WarStatus? war = null;
        try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
        catch { /* не смогли узнать текущую неделю — тогда учитываем все */ }
        if (war is not null) byWeek.Remove((war.SeasonId, war.SectionIndex));

        return byWeek
            .OrderByDescending(kv => kv.Key.Season).ThenByDescending(kv => kv.Key.Section)
            .Take(WeeksWindow)
            .Select(kv => kv.Value)
            .ToList();
    }

    /// <summary>Во сколько боёв игрок укладывался и насколько близко к дедлайну.</summary>
    private record BattleStats
    {
        public int Total { get; set; }
        public int LastMinute { get; set; }
        public double SumHoursBeforeEnd { get; set; }
    }

    /// <summary>
    /// Разбирает журнал боёв: военный день клана заканчивается в одно и то же время UTC,
    /// поэтому «за сколько до конца» считается от ближайшего такого момента после боя.
    /// </summary>
    private async Task<Dictionary<string, BattleStats>> BuildBattleStatsAsync(Clan clan, CancellationToken ct)
    {
        var settings = NotificationSettings.Parse(clan.NotificationSettingsJson);
        var endMinute = settings.WarEndMinuteUtc is int m && m >= 0 && m < 1440 ? m : DefaultWarEndMinuteUtc;

        var stats = new Dictionary<string, BattleStats>(StringComparer.OrdinalIgnoreCase);

        var since = DateTime.UtcNow.AddDays(-7 * WeeksWindow);
        List<WarBattle> battles;
        try { battles = await warBattles.GetSinceAsync(clan.Id, since, ct); }
        catch { return stats; }

        foreach (var b in battles)
        {
            var end = new DateTime(b.BattleTimeUtc.Year, b.BattleTimeUtc.Month, b.BattleTimeUtc.Day,
                endMinute / 60, endMinute % 60, 0, DateTimeKind.Utc);
            if (end <= b.BattleTimeUtc) end = end.AddDays(1);

            var hoursLeft = (end - b.BattleTimeUtc).TotalHours;

            if (!stats.TryGetValue(b.PlayerTag, out var s)) stats[b.PlayerTag] = s = new BattleStats();
            s.Total++;
            s.SumHoursBeforeEnd += hoursLeft;
            if (hoursLeft <= LastMinuteHours) s.LastMinute++;
        }
        return stats;
    }
}
