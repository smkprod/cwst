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
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            var resp = await http.GetAsync($"clans/{Encode(clanTag)}/currentriverrace", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                throw new InvalidOperationException(
                    "CR API отклонил ключ (403). Ключ привязан к IP — создай новый на developer.clashroyale.com " +
                    "с IP сервера (Render: Settings → Outbound IPs) и обнови CLASH_ROYALE_API_TOKEN.");
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

            return new WarStatus
            {
                ClanTag = race.Clan.Tag,
                PeriodType = race.PeriodType ?? "training",
                PeriodIndex = dayIndex,
                SeasonId = race.SeasonId,
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
                        // c.Fame во время военного дня часто 0 — реальная слава в сумме участников
                        Fame = Math.Max(c.Fame, c.Participants?.Sum(p => p.Fame) ?? 0),
                        PeriodPoints = c.PeriodPoints,
                        ParticipantCount = c.Participants?.Count ?? 0,
                        DecksUsedToday = c.Participants?.Sum(p => p.DecksUsedToday) ?? 0,
                        DecksUsed = c.Participants?.Sum(p => p.DecksUsed) ?? 0,
                        IsFinished = !string.IsNullOrEmpty(c.FinishTime),
                    }).ToList()
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
        [property: JsonPropertyName("clans")] List<RaceClan>? Clans);

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

    private async Task<ClanMembersResponse?> GetCachedMembersAsync(string clanTag, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync($"clanrole:{clanTag}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var resp = await http.GetAsync($"clans/{Encode(clanTag)}/members", ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ClanMembersResponse>(cancellationToken: ct);
        });
    }

    private record NamedEntity([property: JsonPropertyName("name")] string Name);

    private record PlayerWithClan([property: JsonPropertyName("clan")] ClanRef? Clan);
    private record ClanRef([property: JsonPropertyName("tag")] string Tag);
    private record ClanMembersResponse([property: JsonPropertyName("items")] List<ClanMember>? Items);
    private record ClanMember(
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("role")] string Role);
}
