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
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().TelegramUserId, StringComparer.OrdinalIgnoreCase);

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

        // История (Pro): стрики, DNA, надёжность и EWMA-профили прогноза.
        // Загружается ДО enrichment — EWMA передаётся в ProjectPlayer.
        var history = new Dictionary<string, PlayerHistoryStats>(StringComparer.OrdinalIgnoreCase);
        if (clan is not null && plan == Domain.Enums.PlanTier.Pro)
            history = await ComputeHistoryStatsAsync(clan.Id, war, ct);

        // Расчёт по каждому игроку + прогноз
        var enriched = roster.Select(p =>
            {
                p.TelegramUserId = linked.GetValueOrDefault(p.PlayerTag);
                p.Status = Classify(p.DecksUsedToday, hoursLeft, war.IsWarDay);
                var histProfile = history.TryGetValue(p.PlayerTag, out var hs) ? hs.ForecastProfile : null;
                var proj = forecast.ProjectPlayer(p, war, hoursLeft, clanAvgFamePerAttack,
                    canStillAttack: true, history: histProfile);
                return (Participant: p, Projection: proj);
            }).ToList();

        // Ранги по славе (1 = больше всех)
        var ranked = enriched
            .OrderByDescending(x => x.Participant.Fame)
            .Select((x, i) => (x.Participant, x.Projection, Rank: i + 1))
            .ToList();

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
                ConsecutiveWars: history.TryGetValue(x.Participant.PlayerTag, out var h) ? h.Streak : 0,
                Role: RoleLabel(memberRoles.GetValueOrDefault(x.Participant.PlayerTag)),
                DnaLabel: history.TryGetValue(x.Participant.PlayerTag, out var h2) ? h2.DnaLabel : null,
                ReliabilityScore: history.TryGetValue(x.Participant.PlayerTag, out var h3) ? h3.Reliability : 0))
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

        var clanForecast = forecast.BuildClanForecast(war, playerDtos, hoursLeft);

        var race = await BuildRaceAsync(war, clanAvgFamePerAttack, ct);

        // Pro-аналитика: шанс победы + здоровье клана
        ClanInsightsDto? insights = null;
        if (plan == Domain.Enums.PlanTier.Pro)
            insights = BuildInsights(war, playerDtos, race, history);

        // Журнал прошлых войн: места кланов и очки (официальный /riverracelog)
        List<WarLogWeekDto> warLog = [];
        try
        {
            var raceLog = await crApi.GetRiverRaceLogAsync(war.ClanTag, ct);
            warLog = raceLog.Take(6).Select(w => new WarLogWeekDto(
                SeasonId: w.SeasonId,
                SectionIndex: w.SectionIndex,
                IsColosseum: w.IsColosseum,
                Standings: w.Standings.Select(s => new WarLogClanDto(
                    Rank: s.Rank,
                    Name: s.ClanName,
                    Fame: s.Fame,
                    TrophyChange: s.TrophyChange,
                    IsOurClan: string.Equals(s.ClanTag, war.ClanTag, StringComparison.OrdinalIgnoreCase),
                    Players: s.Participants
                        .OrderByDescending(p => p.Fame)
                        .Select(p => new WarLogPlayerDto(p.Name, p.Fame, p.DecksUsed))
                        .ToList()))
                    .ToList()))
                .ToList();
        }
        catch { /* журнал не критичен */ }

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
            Players: playerDtos,
            Insights: insights,
            WarLog: warLog);
    }

    /// <summary>
    /// Таблица гонки: все кланы недели с прогнозом финальных медалей (стиль RoyaleAPI:
    /// прогноз = текущие медали + оставшиеся колоды × среднее медалей за колоду × активность).
    /// </summary>
    private async Task<List<RaceClanDto>> BuildRaceAsync(
        Domain.Entities.WarStatus war, double ourAvgFamePerAttack, CancellationToken ct)
    {
        // КВ-трофеи каждого клана гонки (кэш 1 час в клиенте; ошибки не критичны)
        var trophies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in war.RaceClans)
        {
            try { trophies[c.Tag] = await crApi.GetClanWarTrophiesAsync(c.Tag, ct) ?? 0; }
            catch { trophies[c.Tag] = 0; }
        }

        var rows = war.RaceClans.Select(c =>
        {
            var rosterSize = Math.Min(Math.Max(c.ParticipantCount, 1), MaxClanMembers);
            var maxDecksToday = war.IsWarDay ? rosterSize * DecksPerDayPerPlayer : 0;
            var isOurs = string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase);

            // Средняя слава за СЕГОДНЯШНЮЮ атаку: TodayFame / decksUsedToday.
            // TodayFame = clan.fame из JSON CR API = медали только за сегодня (не накопленные!).
            // Формула cwstats: не мешаем вчерашние дни.
            double avg;
            if (war.IsWarDay && c.DecksUsedToday > 0)
                avg = (double)c.TodayFame / c.DecksUsedToday;
            else if (isOurs && ourAvgFamePerAttack > 0)
                avg = ourAvgFamePerAttack; // фолбэк для нас при 0 атак сегодня
            else
                avg = 150; // cold start для соперников
            avg = Math.Clamp(avg, 100, 250);

            // Прогноз = avg × maxDecksToday (стиль cwstats: предполагаем полную отдачу).
            var projected = c.IsFinished
                ? c.TodayFame
                : war.IsWarDay
                    ? (int)Math.Round(avg * maxDecksToday)
                    : 0;

            return new
            {
                c.Tag, c.Name, c.Fame, TodayFame = c.TodayFame, BoatPoints = c.BoatPoints, c.IsFinished,
                c.DecksUsedToday, MaxDecksToday = maxDecksToday,
                Avg = Math.Round(avg, 1), Projected = projected, IsOurs = isOurs,
            };
        })
        .OrderByDescending(r => r.IsFinished)
        .ThenByDescending(r => r.Fame)
        .ThenByDescending(r => r.TodayFame)
        .ThenByDescending(r => r.DecksUsedToday)
        .Select((r, i) => new RaceClanDto(
            Tag: r.Tag,
            Name: r.Name,
            Position: i + 1,
            Fame: r.Fame,
            TodayFame: r.TodayFame,
            BoatPoints: r.BoatPoints,
            ProjectedFame: r.Projected,
            AvgFamePerAttack: r.Avg,
            DecksUsedToday: r.DecksUsedToday,
            MaxDecksToday: r.MaxDecksToday,
            WarTrophies: trophies.GetValueOrDefault(r.Tag, 0),
            IsOurClan: r.IsOurs,
            IsFinished: r.IsFinished))
        .ToList();

        return rows;
    }

    /// <summary>Накопленная история игрока: стрик, надёжность, DNA-архетип и EWMA-профиль прогноза.</summary>
    private record PlayerHistoryStats(
        int Streak,
        int Reliability,
        string? DnaLabel,
        WarForecastService.PlayerHistoryProfile? ForecastProfile = null);

    /// <summary>Обёртка для хранения медалей + колод в унифицированном weekly map.</summary>
    private record WeekPlayerData(int Fame, int Decks);

    private async Task<Dictionary<string, PlayerHistoryStats>> ComputeHistoryStatsAsync(
        int clanId, Domain.Entities.WarStatus war, CancellationToken ct)
    {
        var cacheKey = $"histstats:{clanId}:{war.SeasonId}:{war.SectionIndex}";
        if (cache.TryGetValue(cacheKey, out Dictionary<string, PlayerHistoryStats>? cached) && cached is not null)
            return cached;

        var recent = await snapshots.GetByClanAsync(clanId, weeks: 12, ct);

        var weekFinals = recent
            .GroupBy(s => (s.SeasonId, s.SectionIndex))
            .Select(g => g.OrderByDescending(s => s.PeriodIndex).First())
            .Where(s => !(s.SeasonId == war.SeasonId && s.SectionIndex == war.SectionIndex))
            .ToList();

        List<Domain.Entities.RiverRaceLogWeek> raceLog = [];
        try { raceLog = await crApi.GetRiverRaceLogAsync(war.ClanTag, ct); }
        catch { /* журнал не критичен */ }

        // Унифицированные финалы: (сезон, неделя) → playerTag → (Fame, DecksUsed)
        // Своих снапшоты приоритетнее — они содержат точные DecksUsed за войну.
        var weekMaps = new Dictionary<(int Season, int Section), Dictionary<string, WeekPlayerData>>();

        foreach (var s in weekFinals)
            weekMaps[(s.SeasonId, s.SectionIndex)] = s.Players
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                    g => new WeekPlayerData(g.First().Fame, g.First().DecksUsed),
                    StringComparer.OrdinalIgnoreCase);

        foreach (var w in raceLog)
        {
            if (weekMaps.ContainsKey((w.SeasonId, w.SectionIndex))) continue;
            var ours = w.Standings.FirstOrDefault(st =>
                string.Equals(st.ClanTag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
            if (ours is null) continue;
            weekMaps[(w.SeasonId, w.SectionIndex)] = ours.Participants
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                    g => new WeekPlayerData(g.First().Fame, g.First().DecksUsed),
                    StringComparer.OrdinalIgnoreCase);
        }

        var weeks = weekMaps
            .Where(kv => !(kv.Key.Season == war.SeasonId && kv.Key.Section == war.SectionIndex))
            .OrderByDescending(kv => kv.Key.Season).ThenByDescending(kv => kv.Key.Section)
            .Take(12)
            .Select(kv => (IReadOnlyDictionary<string, (int Fame, int Decks)>)
                kv.Value.ToDictionary(kv2 => kv2.Key, kv2 => (kv2.Value.Fame, kv2.Value.Decks),
                    StringComparer.OrdinalIgnoreCase))
            .ToList();

        var result = new Dictionary<string, PlayerHistoryStats>(StringComparer.OrdinalIgnoreCase);

        var allWeekFames = weeks
            .SelectMany(w => w.Values.Where(v => v.Fame > 0).Select(v => (double)v.Fame))
            .ToList();
        var clanAvgWeekFame = allWeekFames.Count > 0 ? allWeekFames.Average() : 0;

        // Строим EWMA-профили для прогноза одним батчем
        var allTags = war.Participants.Select(p => p.PlayerTag).ToList();
        var profiles = WarForecastService.BuildHistoryProfiles(weeks, allTags);

        foreach (var p in war.Participants)
        {
            // Стрик: недели подряд с участием, начиная с последней завершённой
            int streak = 0;
            foreach (var week in weeks)
            {
                if (!week.TryGetValue(p.PlayerTag, out var pd) || pd.Fame == 0) break;
                streak++;
            }

            // DNA: участие и стабильность за последние 8 завершённых недель
            var dnaWindow = weeks.Take(8).ToList();
            var fames = dnaWindow
                .Select(w => (double)(w.TryGetValue(p.PlayerTag, out var pd) ? pd.Fame : 0))
                .ToList();
            var played = fames.Count(f => f > 0);

            var profile = profiles.TryGetValue(p.PlayerTag, out var pr) ? pr : null;

            if (dnaWindow.Count < 2 || played == 0)
            {
                result[p.PlayerTag] = new PlayerHistoryStats(
                    streak, 0, dnaWindow.Count >= 2 ? "Новичок 🌱" : null, profile);
                continue;
            }

            var participation = (double)played / dnaWindow.Count;
            var playedFames = fames.Where(f => f > 0).ToList();
            var mean = playedFames.Average();
            var std = playedFames.Count > 1
                ? Math.Sqrt(playedFames.Sum(f => (f - mean) * (f - mean)) / playedFames.Count)
                : 0;
            var cv = mean > 0 ? std / mean : 0;

            var reliability = (int)Math.Round(100 * (0.65 * participation + 0.35 * (1 - Math.Min(cv, 1))));

            var dna =
                participation >= 0.75 && clanAvgWeekFame > 0 && mean >= clanAvgWeekFame * 1.3 ? "Тащер 💪"
                : participation >= 0.75 && cv <= 0.35 ? "Надёжный 🛡"
                : participation <= 0.35 ? "Балласт 😴"
                : cv > 0.5 ? "Нестабильный 🎲"
                : "Стабильный ⚖️";

            result[p.PlayerTag] = new PlayerHistoryStats(
                streak, Math.Clamp(reliability, 0, 100), dna, profile);
        }

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        });
        return result;
    }

    /// <summary>
    /// Pro-аналитика. Шанс победы — эвристика: разница прогнозов наша/лучший соперник,
    /// нормированная на неопределённость оставшихся атак (логистическая функция).
    /// Здоровье клана — 4 фактора 0..100 со средневзвешенным итогом.
    /// </summary>
    private static ClanInsightsDto BuildInsights(
        Domain.Entities.WarStatus war,
        List<PlayerStatusDto> players,
        List<RaceClanDto> race,
        Dictionary<string, PlayerHistoryStats> history)
    {
        // --- Шанс победы (только в военные дни) ---
        int? winChance = null, winChanceDown = null;
        string? topRivalName = null;

        var ours = race.FirstOrDefault(r => r.IsOurClan);
        var rivals = race.Where(r => !r.IsOurClan).ToList();
        if (war.IsWarDay && ours is not null && rivals.Count > 0)
        {
            var bestRival = rivals
                .OrderByDescending(r => r.IsFinished ? r.Fame : r.ProjectedFame)
                .First();
            topRivalName = bestRival.Name;
            double rivalProj = bestRival.IsFinished ? bestRival.Fame : bestRival.ProjectedFame;

            // Неопределённость ≈ треть ещё не набранной (прогнозной) славы, минимум 400
            double uOur = Math.Max(400, (ours.ProjectedFame - ours.Fame) * 0.35);
            double uRiv = Math.Max(400, (rivalProj - bestRival.Fame) * 0.35);
            double sigma = Math.Sqrt(uOur * uOur + uRiv * uRiv);

            winChance = ToChance((ours.ProjectedFame - rivalProj) / sigma);

            // Сценарий: не доигравшие сегодня игроки вообще больше не сыграют на неделе
            var lostFuture = players
                .Where(p => p.Status != "played")
                .Sum(p => Math.Max(0, p.ProjectedWeekFame - p.Fame));
            winChanceDown = ToChance((ours.ProjectedFame - lostFuture - rivalProj) / sigma);
        }

        // --- Здоровье клана ---
        var roster = Math.Max(1, players.Count);
        var activity = (int)Math.Round(100.0 * players.Count(p => p.DecksUsed > 0) / roster);

        var discipline = war.IsWarDay
            ? (int)Math.Round(100.0 * players.Count(p => p.DecksUsedToday >= 4) / roster)
            : activity; // на тренировке судим по активности

        var withHistory = players.Where(p => history.ContainsKey(p.PlayerTag)).ToList();

        // Только игроки с реальными данными (Reliability > 0 = минимум 2 недели истории)
        var reliableData = withHistory.Where(p => history[p.PlayerTag].Reliability > 0).ToList();
        var attendance = reliableData.Count > 0
            ? (int)Math.Round(reliableData.Average(p => history[p.PlayerTag].Reliability))
            : 50; // нейтральное значение, пока данных < 2 недель

        // Костяк: доля игроков со стриком ≥ 2 среди тех, кто вообще участвовал (Streak > 0)
        var streakData = withHistory.Where(p => history[p.PlayerTag].Streak > 0).ToList();
        var core = streakData.Count > 0
            ? (int)Math.Round(100.0 * streakData.Count(p => history[p.PlayerTag].Streak >= 2) / streakData.Count)
            : 50; // нейтральное значение, пока нет истории

        var health = (int)Math.Round(0.30 * activity + 0.25 * discipline + 0.25 * attendance + 0.20 * core);
        health = Math.Clamp(health, 0, 100);

        var label = health >= 75 ? "Сильный клан"
            : health >= 50 ? "Стабильный"
            : health >= 30 ? "Нестабильный"
            : "Критично";

        return new ClanInsightsDto(
            WinChance: winChance,
            WinChanceIfSlackersOut: winChanceDown,
            TopRivalName: topRivalName,
            HealthScore: health,
            HealthLabel: label,
            Factors:
            [
                new HealthFactorDto("Активность", activity),
                new HealthFactorDto("Дисциплина 4/4", discipline),
                new HealthFactorDto("Надёжность состава", attendance),
                new HealthFactorDto("Костяк (стрики)", core),
            ]);
    }

    /// <summary>Логистическая функция → проценты 3..97 (никогда не обещаем 0/100).</summary>
    private static int ToChance(double z) =>
        Math.Clamp((int)Math.Round(100.0 / (1.0 + Math.Exp(-1.6 * z))), 3, 97);

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
