using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

public class GetClanStatusUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    IMemoryCache cache,
    WarForecastService forecast)
{
    /// <summary>За сколько часов до конца дня жёлтый статус превращается в красный.</summary>
    private const int RedThresholdHours = 4;
    private const int DecksPerDayPerPlayer = 4;

    /// <summary>В клане максимум 50 человек; лишние записи в API — ушедшие из клана.</summary>
    private const int MaxClanMembers = 50;

    public async Task<ClanStatusDto?> ExecuteAsync(string clanTag, CancellationToken ct = default)
    {
        var war = await crApi.GetCurrentWarAsync(clanTag, ct);
        if (war is null) return null;

        var clan = await clans.GetByTagAsync(clanTag, ct);
        var linked = clan is null
            ? []
            : (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .ToDictionary(p => p.PlayerTag, p => p.TelegramUserId);

        var now = DateTime.UtcNow;
        var hoursLeft = Math.Max(0, (int)war.TimeLeft(now).TotalHours);

        // Уточняем военные колоды по снапшоту первого военного дня:
        // CR API в DecksUsed считает и тренировочные бои, занижая «славу/атаку»
        if (clan is not null && war.IsWarDay && war.PeriodIndex > 3)
        {
            var firstWarDay = await snapshots.GetSnapshotAsync(
                clan.Id, war.SeasonId, war.SectionIndex, periodIndex: 3, ct);
            WarForecastService.RefineWarDecks(war, firstWarDay);
        }

        // Получаем актуальный состав клана из API (кэш 5 мин), чтобы не показывать
        // ушедших участников — в CR API их может быть >50 в списке войны.
        var memberRoles = await crApi.GetClanMemberRolesAsync(war.ClanTag, ct);
        HashSet<string> rosterTags;
        if (memberRoles.Count > 0)
        {
            rosterTags = memberRoles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            // Фолбэк: 50 самых активных (если API клана недоступен)
            rosterTags = war.Participants
                .OrderByDescending(p => p.DecksUsedToday)
                .ThenByDescending(p => p.DecksUsed)
                .ThenByDescending(p => p.Fame)
                .Take(MaxClanMembers)
                .Select(p => p.PlayerTag)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // Фильтруем участников к текущему составу
        var roster = war.Participants.Where(p => rosterTags.Contains(p.PlayerTag)).ToList();

        // Средние медали за военную атаку (фолбэк 150 = 50% винрейта)
        var totalDecksUsed = roster.Sum(p => p.DecksUsed);
        var totalWarDecks = roster.Sum(p => p.WarDecksUsed);
        var totalFame = roster.Sum(p => p.Fame);
        var clanAvgFamePerAttack = totalWarDecks > 0 ? (double)totalFame / totalWarDecks : 150.0;

        // Гейтинг плана
        var plan = clan?.EffectivePlan(now) ?? Domain.Enums.PlanTier.Free;

        // Расчёт по каждому игроку + прогноз
        var enriched = roster.Select(p =>
            {
                p.TelegramUserId = linked.GetValueOrDefault(p.PlayerTag);
                p.Status = Classify(p.DecksUsedToday, hoursLeft, war.IsWarDay);
                var proj = forecast.ProjectPlayer(p, war, hoursLeft, clanAvgFamePerAttack,
                    canStillAttack: true);
                return (Participant: p, Projection: proj);
            }).ToList();

        // Ранги по славе (1 = больше всех)
        var ranked = enriched
            .OrderByDescending(x => x.Participant.Fame)
            .Select((x, i) => (x.Participant, x.Projection, Rank: i + 1))
            .ToList();

        // Серии побед (Pro): для каждого игрока — сколько недель подряд он участвовал
        var streaks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (clan is not null && plan == Domain.Enums.PlanTier.Pro)
            streaks = await ComputeStreaksAsync(clan.Id, war, ct);

        var playerDtos = ranked
            .Select(x => new PlayerStatusDto(
                PlayerTag: x.Participant.PlayerTag,
                Name: x.Participant.Name,
                DecksUsedToday: x.Participant.DecksUsedToday,
                DecksUsed: x.Participant.DecksUsed,
                WarDecksUsed: x.Participant.WarDecksUsed,
                Fame: x.Participant.Fame,
                RepairPoints: x.Participant.RepairPoints,
                BoatAttacks: x.Participant.BoatAttacks,
                AvgFamePerAttack: Math.Round(x.Projection.AvgFamePerAttack, 1),
                ProjectedDayFame: x.Projection.ProjectedDayFame,
                ProjectedWeekFame: x.Projection.ProjectedWeekFame,
                Rank: x.Rank,
                Status: ToApiString(x.Participant.Status),
                IsLinked: x.Participant.TelegramUserId is not null,
                ConsecutiveWars: streaks.GetValueOrDefault(x.Participant.PlayerTag, 0),
                Role: RoleLabel(memberRoles.GetValueOrDefault(x.Participant.PlayerTag))))
            // в основном списке UI хочет видеть не сыгравших сверху
            .OrderBy(p => p.Status == "played" ? 1 : p.Status == "timeLeft" ? 0 : -1)
            .ThenByDescending(p => p.Fame)
            .ToList();

        var stats = new ClanStatsDto(
            TotalFame: totalFame,
            TotalRepairPoints: roster.Sum(p => p.RepairPoints),
            TotalDecksUsedToday: roster.Sum(p => p.DecksUsedToday),
            TotalDecksUsedWeek: totalDecksUsed,
            MaxDecksToday: roster.Count * DecksPerDayPerPlayer,
            ActivePlayers: roster.Count(p => p.DecksUsed > 0),
            PlayersPlayed: playerDtos.Count(p => p.Status == "played"),
            PlayersNotPlayed: playerDtos.Count(p => p.Status == "notPlayed"),
            AvgFamePerAttack: Math.Round(clanAvgFamePerAttack, 1));

        // Прогноз — Pro-фича; кланам не из БД (просмотр по тегу) — Free
        var clanForecast = plan == Domain.Enums.PlanTier.Pro
            ? forecast.BuildClanForecast(war, playerDtos, hoursLeft)
            : null;

        var race = BuildRace(war, clanAvgFamePerAttack);

        return new ClanStatusDto(
            ClanTag: war.ClanTag,
            ClanName: clan?.Name ?? war.ClanTag,
            PeriodType: war.PeriodType,
            PeriodIndex: war.PeriodIndex,
            DayEndsAtUtc: war.DayEndsAtUtc,
            HoursLeft: hoursLeft,
            Plan: plan == Domain.Enums.PlanTier.Pro ? "pro" : "free",
            Stats: stats,
            Forecast: clanForecast,
            Race: race,
            Players: playerDtos);
    }

    /// <summary>
    /// Таблица гонки: все кланы недели с прогнозом финальной славы (стиль RoyaleAPI:
    /// прогноз = текущая слава + оставшиеся колоды × средняя слава за колоду × активность).
    /// </summary>
    private List<RaceClanDto> BuildRace(Domain.Entities.WarStatus war, double ourAvgFamePerAttack)
    {
        var remainingWarDays = war.PeriodIndex < 3 ? 4 : Math.Clamp(6 - war.PeriodIndex, 0, 4);

        var rows = war.RaceClans.Select(c =>
        {
            var rosterSize = Math.Min(Math.Max(c.ParticipantCount, 1), MaxClanMembers);
            var maxDecksToday = war.IsWarDay ? rosterSize * DecksPerDayPerPlayer : 0;
            var isOurs = string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase);

            // Средняя слава за колоду: для своего клана — точная (военные колоды),
            // для соперников — оценка по неделе в игровых границах 100..250
            double avg;
            if (isOurs)
                avg = ourAvgFamePerAttack;
            else if (war.IsWarDay && war.PeriodIndex == 3 && c.DecksUsedToday > 0)
                avg = (double)c.Fame / c.DecksUsedToday; // первый военный день — точно
            else if (c.DecksUsed > 0)
                avg = (double)c.Fame / c.DecksUsed;      // включает тренировку — нижняя оценка
            else
                avg = 150;
            avg = Math.Clamp(avg, 100, 250);

            // Активность: какую долю сегодняшних колод клан реально использует
            var participation = maxDecksToday > 0
                ? Math.Clamp((double)c.DecksUsedToday / maxDecksToday, 0.4, 1.0)
                : 0.7;

            var decksLeftToday = Math.Max(0, maxDecksToday - c.DecksUsedToday);
            var futureDecks = remainingWarDays * rosterSize * DecksPerDayPerPlayer;
            var projected = c.IsFinished
                ? c.Fame
                : c.Fame + (int)Math.Round((decksLeftToday + futureDecks) * participation * avg);

            return new
            {
                c.Tag, c.Name, c.Fame, c.PeriodPoints, c.IsFinished,
                c.DecksUsedToday, MaxDecksToday = maxDecksToday,
                Avg = Math.Round(avg, 1), Projected = projected, IsOurs = isOurs,
            };
        })
        // Места: финишировавшие выше всех, дальше по славе
        .OrderByDescending(r => r.IsFinished)
        .ThenByDescending(r => r.Fame)
        .Select((r, i) => new RaceClanDto(
            Tag: r.Tag,
            Name: r.Name,
            Position: i + 1,
            Fame: r.Fame,
            PeriodPoints: r.PeriodPoints,
            ProjectedFame: r.Projected,
            AvgFamePerAttack: r.Avg,
            DecksUsedToday: r.DecksUsedToday,
            MaxDecksToday: r.MaxDecksToday,
            IsOurClan: r.IsOurs,
            IsFinished: r.IsFinished))
        .ToList();

        return rows;
    }

    private async Task<Dictionary<string, int>> ComputeStreaksAsync(
        int clanId, Domain.Entities.WarStatus war, CancellationToken ct)
    {
        // Стрики зависят только от ФИНАЛОВ прошлых недель — текущая неделя исключается,
        // поэтому результат можно смело кэшировать (инвалидация — по смене недели + TTL)
        var cacheKey = $"streaks:{clanId}:{war.SeasonId}:{war.SectionIndex}";
        if (cache.TryGetValue(cacheKey, out Dictionary<string, int>? cached) && cached is not null)
            return cached;

        var result = await ComputeStreaksUncachedAsync(clanId, war, ct);
        cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        return result;
    }

    private async Task<Dictionary<string, int>> ComputeStreaksUncachedAsync(
        int clanId, Domain.Entities.WarStatus war, CancellationToken ct)
    {
        // Берём последние 12 недель снапшотов клана
        var recent = await snapshots.GetByClanAsync(clanId, weeks: 12, ct);

        // Группируем по неделям (SeasonId+SectionIndex), в каждой берём финальный снимок
        var weekFinals = recent
            .GroupBy(s => (s.SeasonId, s.SectionIndex))
            .Select(g => g.OrderByDescending(s => s.PeriodIndex).First())
            // Пропускаем текущую ещё-не-финальную неделю
            .Where(s => !(s.SeasonId == war.SeasonId && s.SectionIndex == war.SectionIndex))
            .OrderByDescending(s => s.SeasonId).ThenByDescending(s => s.SectionIndex)
            .ToList();

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in war.Participants)
        {
            int streak = 0;
            foreach (var week in weekFinals)
            {
                var entry = week.Players.FirstOrDefault(x =>
                    string.Equals(x.PlayerTag, p.PlayerTag, StringComparison.OrdinalIgnoreCase));
                if (entry is null || entry.Fame == 0) break;
                streak++;
            }
            result[p.PlayerTag] = streak;
        }
        return result;
    }

    private static string? RoleLabel(string? apiRole) => apiRole switch
    {
        "leader" => "Лидер",
        "coLeader" => "Соруководитель",
        "elder" => "Старейшина",
        _ => null   // "member" — не отображаем
    };

    public static WarPlayStatus Classify(int decksUsed, int hoursLeft, bool isWarDay)
    {
        if (!isWarDay) return WarPlayStatus.TimeLeft;            // тренировка — дедлайна нет
        if (decksUsed >= 4) return WarPlayStatus.Played;          // ✅
        if (hoursLeft <= RedThresholdHours) return WarPlayStatus.NotPlayed; // ❌
        return WarPlayStatus.TimeLeft;                            // ⏳
    }

    private static string ToApiString(WarPlayStatus s) => s switch
    {
        WarPlayStatus.Played => "played",
        WarPlayStatus.NotPlayed => "notPlayed",
        _ => "timeLeft"
    };
}
