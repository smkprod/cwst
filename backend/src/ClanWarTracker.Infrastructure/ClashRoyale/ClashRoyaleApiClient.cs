using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Infrastructure.ClashRoyale;

/// <summary>
/// Клиент официального Clash Royale API (https://developer.clashroyale.com).
/// Тег в URL кодируется: # -> %23. Ответы кэшируются на 2 минуты,
/// чтобы не упереться в rate limit при 100+ кланах.
/// </summary>
public class ClashRoyaleApiClient(HttpClient http, IMemoryCache cache) : IClashRoyaleApi
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<WarStatus?> GetCurrentWarAsync(string clanTag, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync($"war:{clanTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            var resp = await http.GetAsync($"clans/{Encode(clanTag)}/currentriverrace", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new InvalidOperationException(
                    "CR API отклонил ключ (403). Ключ привязан к IP — создай новый на developer.clashroyale.com " +
                    "с IP сервера (Render: Settings → Outbound IPs) и обнови CLASH_ROYALE_API_TOKEN.");
            if ((int)resp.StatusCode >= 500 || resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new HttpRequestException(
                    $"Clash Royale API временно недоступен ({(int)resp.StatusCode}). Попробуйте через минуту.",
                    null, resp.StatusCode);
            resp.EnsureSuccessStatusCode();

            var race = await resp.Content.ReadFromJsonAsync<RiverRaceResponse>(cancellationToken: ct);
            if (race?.Clan is null) return null;

            // ВАЖНО: в CR API periodIndex сквозной за сезон (неделя*7 + день, напр. 17),
            // а вся наша логика ждёт день недели гонки 0..6 (0-2 тренировка, 3-6 война).
            var dayIndex = ((race.PeriodIndex % 7) + 7) % 7;

            var isWarDay = (race.PeriodType ?? "training") is "warDay" or "colosseum";
            // Первый военный день: военные колоды = сыгранные сегодня (точно).
            // Дальше WarDecksUsed уточняется по снапшоту первого дня в Application-слое.
            var isFirstWarDay = isWarDay && dayIndex == 3;

            var realSeasonId = await ResolveRealSeasonIdAsync(clanTag, race.SectionIndex, race.SeasonId, ct);

            return new WarStatus
            {
                ClanTag = race.Clan.Tag,
                PeriodType = race.PeriodType ?? "training",
                PeriodIndex = dayIndex,
                SeasonId = realSeasonId,
                SectionIndex = race.SectionIndex,
                DayEndsAtUtc = ComputeDayEnd(race.PeriodIndex),
                Participants = (race.Clan.Participants ?? []).Select(p => new WarParticipant
                {
                    PlayerTag = p.Tag,
                    Name = p.Name,
                    DecksUsedToday = p.DecksUsedToday,
                    DecksUsed = p.DecksUsed,
                    WarDecksUsed = !isWarDay ? 0 : isFirstWarDay ? p.DecksUsedToday : p.DecksUsed,
                    Fame = p.Fame,
                    RepairPoints = p.RepairPoints,
                    BoatAttacks = p.BoatAttacks
                }).ToList(),
                RaceClans = (race.Clans ?? [])
                    .Where(c => c?.Tag is not null)
                    .Select(c => new RaceClanStanding
                    {
                        Tag = c!.Tag,
                        Name = c.Name,
                        // ВАЖНО (CR API): periodPoints = медали за СЕГОДНЯ, clan.fame = очки лодки.
                        TodayFame = c.PeriodPoints,     // медали за сегодняшний бой
                        BoatPoints = c.Fame,            // очки лодки (boat) за сегодня
                        // Fame (накопленная за неделю) = сумма по участникам
                        Fame = c.Participants?.Sum(p => p.Fame) ?? c.PeriodPoints,
                        ParticipantCount = c.Participants?.Count ?? 0,
                        DecksUsedToday = c.Participants?.Sum(p => p.DecksUsedToday) ?? 0,
                        DecksUsed = c.Participants?.Sum(p => p.DecksUsed) ?? 0,
                        IsFinished = !string.IsNullOrEmpty(c.FinishTime),
                    }).ToList(),
                // Официальный по-дневный лог для нашего клана (periodLogs[].items[] где clan.tag == наш).
                DayLogs = (race.PeriodLogs ?? [])
                    .Select(pl =>
                    {
                        var mine = pl.Items?.FirstOrDefault(i =>
                            string.Equals(i.Clan?.Tag, race.Clan!.Tag, StringComparison.OrdinalIgnoreCase));
                        return mine is null ? null : new WarPeriodLog
                        {
                            PeriodIndex = pl.PeriodIndex,
                            DayIndex = ((pl.PeriodIndex % 7) + 7) % 7,
                            PointsEarned = mine.PointsEarned,
                            ProgressEndOfDay = mine.ProgressEndOfDay,
                            EndOfDayRank = mine.EndOfDayRank,
                            NumOfDefensesRemaining = mine.NumOfDefensesRemaining,
                            ProgressEarnedFromDefenses = mine.ProgressEarnedFromDefenses,
                        };
                    })
                    .Where(x => x is not null)
                    .Select(x => x!)
                    .OrderBy(x => x.PeriodIndex)
                    .ToList(),
            };
        });
    }

    public async Task<string?> GetPlayerNameAsync(string playerTag, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"players/{Encode(playerTag)}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(
                "CR API отклонил ключ (403). Ключ привязан к IP — создай новый на developer.clashroyale.com " +
                "с IP сервера (Render: Settings → Outbound IPs) и обнови CLASH_ROYALE_API_TOKEN.");
        if (!resp.IsSuccessStatusCode) return null;
        var player = await resp.Content.ReadFromJsonAsync<NamedEntity>(cancellationToken: ct);
        return player?.Name;
    }

    public async Task<string?> GetClanNameAsync(string clanTag, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"clans/{Encode(clanTag)}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(
                "CR API отклонил ключ (403). Ключ привязан к IP — создай новый на developer.clashroyale.com " +
                "с IP сервера (Render: Settings → Outbound IPs) и обнови CLASH_ROYALE_API_TOKEN.");
        if (!resp.IsSuccessStatusCode) return null;
        var clan = await resp.Content.ReadFromJsonAsync<NamedEntity>(cancellationToken: ct);
        return clan?.Name;
    }

    public async Task<string?> GetPlayerClanTagAsync(string playerTag, CancellationToken ct = default)
    {
        // Кэшируем: вызывается при каждом открытии Mini App (авто-определение клана)
        return await cache.GetOrCreateAsync($"playerclan:{playerTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var resp = await http.GetAsync($"players/{Encode(playerTag)}", ct);
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new InvalidOperationException(
                    "CR API отклонил ключ (403). Ключ привязан к IP — создай новый на developer.clashroyale.com " +
                    "с IP сервера (Render: Settings → Outbound IPs) и обнови CLASH_ROYALE_API_TOKEN.");
            if (!resp.IsSuccessStatusCode) return null;
            var player = await resp.Content.ReadFromJsonAsync<PlayerWithClan>(cancellationToken: ct);
            return player?.Clan?.Tag;
        });
    }

    /// <summary>
    /// /currentriverrace всегда отдаёт seasonId=0 (баг CR API), хотя весь наш дедуп/история
    /// снимков завязаны на реальный сезон. Уточняем по официальному журналу (/riverracelog):
    /// берём сезон самой свежей завершённой недели. Сезон сменился ТОЛЬКО когда текущая
    /// неделя — первая в сезоне (currentSectionIndex == 0): тогда предыдущая завершённая
    /// неделя была колизеем прошлого сезона, и текущий = +1.
    /// ВАЖНО: нельзя завязываться на "section >= 3" — в сезонах из 5 недель колизей это
    /// section 4, и после обычной 4-й недели (section 3) сезон ещё НЕ сменился. Привязка
    /// к section 0 корректна и для 4-, и для 5-недельных сезонов.
    /// Журнал недоступен — отдаём сырое значение как есть.
    /// </summary>
    private async Task<int> ResolveRealSeasonIdAsync(string clanTag, int currentSectionIndex, int rawSeasonId, CancellationToken ct)
    {
        try
        {
            var log = await GetRiverRaceLogAsync(clanTag, ct);
            if (log.Count > 0)
            {
                var newest = log[0]; // журнал отсортирован: свежие недели первыми
                return currentSectionIndex == 0 ? newest.SeasonId + 1 : newest.SeasonId;
            }
        }
        catch { /* журнал не критичен — используем сырое значение */ }
        return rawSeasonId;
    }

    private static string Encode(string tag) => Uri.EscapeDataString(tag); // "#" -> "%23"

    /// <summary>
    /// CR API не возвращает время конца дня напрямую.
    /// День River Race заканчивается каждый день в ~10:00 UTC (смена дня).
    /// MVP-аппроксимация: ближайшие 10:00 UTC в будущем.
    /// </summary>
    private static DateTime ComputeDayEnd(int periodIndex)
    {
        var now = DateTime.UtcNow;
        var todayEnd = new DateTime(now.Year, now.Month, now.Day, 10, 0, 0, DateTimeKind.Utc);
        return now < todayEnd ? todayEnd : todayEnd.AddDays(1);
    }

    // ---- JSON-модели ответа CR API (только нужные поля) ----
    private record RiverRaceResponse(
        [property: JsonPropertyName("periodType")] string? PeriodType,
        [property: JsonPropertyName("periodIndex")] int PeriodIndex,
        [property: JsonPropertyName("seasonId")] int SeasonId,
        [property: JsonPropertyName("sectionIndex")] int SectionIndex,
        [property: JsonPropertyName("clan")] RaceClan? Clan,
        [property: JsonPropertyName("clans")] List<RaceClan>? Clans,
        [property: JsonPropertyName("periodLogs")] List<PeriodLog>? PeriodLogs);

    private record PeriodLog(
        [property: JsonPropertyName("periodIndex")] int PeriodIndex,
        [property: JsonPropertyName("items")] List<PeriodLogEntry>? Items);

    private record PeriodLogEntry(
        [property: JsonPropertyName("clan")] PeriodLogClan? Clan,
        [property: JsonPropertyName("pointsEarned")] int PointsEarned,
        [property: JsonPropertyName("progressStartOfDay")] int ProgressStartOfDay,
        [property: JsonPropertyName("progressEndOfDay")] int ProgressEndOfDay,
        [property: JsonPropertyName("endOfDayRank")] int EndOfDayRank,
        [property: JsonPropertyName("numOfDefensesRemaining")] int NumOfDefensesRemaining,
        [property: JsonPropertyName("progressEarnedFromDefenses")] int ProgressEarnedFromDefenses);

    private record PeriodLogClan([property: JsonPropertyName("tag")] string? Tag);

    private record RaceClan(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("fame")] int Fame,
        [property: JsonPropertyName("periodPoints")] int PeriodPoints,
        [property: JsonPropertyName("finishTime")] string? FinishTime,
        [property: JsonPropertyName("participants")] List<RaceParticipant>? Participants);

    private record RaceParticipant(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("fame")] int Fame,
        [property: JsonPropertyName("repairPoints")] int RepairPoints,
        [property: JsonPropertyName("boatAttacks")] int BoatAttacks,
        [property: JsonPropertyName("decksUsed")] int DecksUsed,
        [property: JsonPropertyName("decksUsedToday")] int DecksUsedToday);

    public async Task<Dictionary<string, string>> GetClanMemberRolesAsync(string clanTag, CancellationToken ct = default)
    {
        var members = await GetCachedMembersAsync(clanTag, ct);
        return members?.Items?.ToDictionary(m => m.Tag, m => m.Role, StringComparer.OrdinalIgnoreCase)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> GetPlayerClanRoleAsync(string clanTag, string playerTag, CancellationToken ct = default)
    {
        var roles = await GetClanMemberRolesAsync(clanTag, ct);
        return roles.TryGetValue(playerTag, out var role) ? role : null;
    }

    public async Task<int?> GetClanWarTrophiesAsync(string clanTag, CancellationToken ct = default)
    {
        // Трофеи меняются только по итогам недели — кэш на час
        return await cache.GetOrCreateAsync($"wartrophies:{clanTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var resp = await http.GetAsync($"clans/{Encode(clanTag)}", ct);
            if (!resp.IsSuccessStatusCode) return (int?)null;
            var clan = await resp.Content.ReadFromJsonAsync<ClanProfile>(cancellationToken: ct);
            return clan?.ClanWarTrophies;
        });
    }

    public async Task<List<RiverRaceLogWeek>> GetRiverRaceLogAsync(string clanTag, CancellationToken ct = default)
    {
        // Журнал меняется только по итогам недели (понедельник ~10:00 UTC) — кэш 6 часов
        var result = await cache.GetOrCreateAsync($"racelog:{clanTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

            var resp = await http.GetAsync($"clans/{Encode(clanTag)}/riverracelog?limit=10", ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Ошибку не кэшируем надолго — попробуем снова через 5 минут
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return new List<RiverRaceLogWeek>();
            }

            var log = await resp.Content.ReadFromJsonAsync<RiverRaceLogResponse>(cancellationToken: ct);
            var weeks = (log?.Items ?? [])
                .Select(w => new RiverRaceLogWeek
                {
                    SeasonId = w.SeasonId,
                    SectionIndex = w.SectionIndex,
                    Standings = (w.Standings ?? [])
                        .Where(s => s?.Clan?.Tag is not null)
                        .OrderBy(s => s!.Rank)
                        .Select(s => new RiverRaceLogStanding
                        {
                            Rank = s!.Rank,
                            TrophyChange = s.TrophyChange,
                            ClanTag = s.Clan!.Tag,
                            ClanName = s.Clan.Name,
                            Fame = s.Clan.Fame,
                            Participants = (s.Clan.Participants ?? [])
                                .Select(p => new RiverRaceLogPlayer
                                {
                                    PlayerTag = p.Tag,
                                    Name = p.Name,
                                    Fame = p.Fame,
                                    DecksUsed = p.DecksUsed,
                                }).ToList(),
                        }).ToList(),
                }).ToList();

            // Колизей = последняя неделя сезона. Число военных недель в сезоне разное (3 или 4),
            // поэтому определяем по контексту: неделя — колизей, если это максимальный section
            // своего сезона И в журнале уже есть неделя более позднего сезона (сезон завершён).
            // У текущего (самого свежего) сезона колизей ещё не отмечаем — он либо не наступил,
            // либо идёт прямо сейчас и в журнал ещё не попал.
            var maxNewerSeason = weeks.Count > 0 ? weeks.Max(w => w.SeasonId) : 0;
            foreach (var w in weeks)
            {
                var maxSectionInSeason = weeks.Where(x => x.SeasonId == w.SeasonId).Max(x => x.SectionIndex);
                w.IsColosseum = w.SeasonId < maxNewerSeason && w.SectionIndex == maxSectionInSeason;
            }
            return weeks;
        });
        return result ?? [];
    }

    private async Task<ClanMembersResponse?> GetCachedMembersAsync(string clanTag, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync($"clanrole:{clanTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var resp = await http.GetAsync($"clans/{Encode(clanTag)}/members", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ClanMembersResponse>(cancellationToken: ct);
        });
    }

    // ---- JSON-модели журнала войн (/riverracelog) ----
    private record RiverRaceLogResponse(
        [property: JsonPropertyName("items")] List<LogItem>? Items);

    private record LogItem(
        [property: JsonPropertyName("seasonId")] int SeasonId,
        [property: JsonPropertyName("sectionIndex")] int SectionIndex,
        [property: JsonPropertyName("standings")] List<LogStanding>? Standings);

    private record LogStanding(
        [property: JsonPropertyName("rank")] int Rank,
        [property: JsonPropertyName("trophyChange")] int TrophyChange,
        [property: JsonPropertyName("clan")] LogClan? Clan);

    private record LogClan(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("fame")] int Fame,
        [property: JsonPropertyName("participants")] List<RaceParticipant>? Participants);

    public async Task<CrPlayerInfo?> GetPlayerInfoAsync(string playerTag, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync($"playerinfo:{playerTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var resp = await http.GetAsync($"players/{Encode(playerTag)}", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new InvalidOperationException(
                    "CR API отклонил ключ (403). Ключ привязан к IP — создай новый на developer.clashroyale.com.");
            if (!resp.IsSuccessStatusCode) return null;
            var data = await resp.Content.ReadFromJsonAsync<PlayerFullResponse>(cancellationToken: ct);
            if (data is null) return null;
            static CrPathOfLegend? MapPol(PathOfLegendResponse? p) =>
                p is null ? null : new CrPathOfLegend { Trophies = p.Trophies, LeagueNumber = p.LeagueNumber, Rank = p.Rank ?? 0 };

            return new CrPlayerInfo
            {
                Tag = data.Tag,
                Name = data.Name,
                ExpLevel = data.ExpLevel,
                Trophies = data.Trophies,
                BestTrophies = data.BestTrophies,
                ClanWarTrophies = data.ClanWarTrophies,
                ClanTag = data.Clan?.Tag,
                ClanName = data.Clan?.Name,
                ArenaName = data.Arena?.Name,
                WarDayWins = data.WarDayWins,
                BattleCount = data.BattleCount,
                ThreeCrownWins = data.ThreeCrownWins,
                CurrentWinLoseStreak = data.CurrentWinLoseStreak,
                CurrentPathOfLegend = MapPol(data.CurrentPathOfLegend),
                BestPathOfLegend = MapPol(data.BestPathOfLegend),
                CurrentFavouriteCard = data.CurrentFavouriteCard?.Name,
                CurrentDeck = (data.CurrentDeck ?? [])
                    .Where(c => c?.IconUrls?.Medium is not null)
                    .Select(c => new CrDeckCard
                    {
                        Name = c!.Name,
                        Level = c.Level,
                        MaxLevel = c.MaxLevel,
                        IconUrl = c.IconUrls!.Medium!,
                    })
                    .ToList(),
                Cards = (data.Cards ?? [])
                    .Where(c => c?.IconUrls?.Medium is not null)
                    .Select(c => new CrCard
                    {
                        Name = c!.Name,
                        Level = c.Level,
                        MaxLevel = c.MaxLevel,
                        IconUrl = c.IconUrls!.Medium!,
                    })
                    .OrderByDescending(c => c.Level)
                    .ThenBy(c => c.Name)
                    .ToList(),
            };
        });
    }

    private record PlayerFullResponse(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("expLevel")] int ExpLevel,
        [property: JsonPropertyName("trophies")] int Trophies,
        [property: JsonPropertyName("bestTrophies")] int BestTrophies,
        [property: JsonPropertyName("clanWarTrophies")] int ClanWarTrophies,
        [property: JsonPropertyName("warDayWins")] int WarDayWins,
        [property: JsonPropertyName("battleCount")] int BattleCount,
        [property: JsonPropertyName("threeCrownWins")] int ThreeCrownWins,
        [property: JsonPropertyName("currentWinLoseStreak")] int CurrentWinLoseStreak,
        [property: JsonPropertyName("currentPathOfLegendSeasonResult")] PathOfLegendResponse? CurrentPathOfLegend,
        [property: JsonPropertyName("bestPathOfLegendSeasonResult")] PathOfLegendResponse? BestPathOfLegend,
        [property: JsonPropertyName("currentFavouriteCard")] CardResponse? CurrentFavouriteCard,
        [property: JsonPropertyName("currentDeck")] List<CardResponse>? CurrentDeck,
        [property: JsonPropertyName("arena")] ArenaRef? Arena,
        [property: JsonPropertyName("clan")] ClanRef? Clan,
        [property: JsonPropertyName("cards")] List<CardResponse>? Cards);

    private record PathOfLegendResponse(
        [property: JsonPropertyName("trophies")] int Trophies,
        [property: JsonPropertyName("leagueNumber")] int LeagueNumber,
        [property: JsonPropertyName("rank")] int? Rank);

    private record ArenaRef([property: JsonPropertyName("name")] string Name);

    private record CardResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("level")] int Level,
        [property: JsonPropertyName("maxLevel")] int MaxLevel,
        [property: JsonPropertyName("iconUrls")] CardIconUrls? IconUrls);

    private record CardIconUrls([property: JsonPropertyName("medium")] string? Medium);

    private record NamedEntity([property: JsonPropertyName("name")] string Name);
    private record ClanProfile([property: JsonPropertyName("clanWarTrophies")] int? ClanWarTrophies);

    private record PlayerWithClan([property: JsonPropertyName("clan")] ClanRef? Clan);
    private record ClanRef(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("name")] string Name = "");
    private record ClanMembersResponse([property: JsonPropertyName("items")] List<ClanMember>? Items);
    private record ClanMember(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("role")] string Role);
}
