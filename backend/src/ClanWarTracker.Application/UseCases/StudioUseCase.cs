using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Студия» блогера: его турниры и ключи виджетов для OBS.
///
/// Виджет открывается в OBS как обычная веб-страница - без Telegram, поэтому
/// доступ к нему даёт только секрет в ссылке. У турнира секрет свой (сетка и
/// текущий матч), у блогера - личный: для общих таблиц лиги и челленджа.
/// </summary>
public class StudioUseCase(
    ITournamentRepository tournaments,
    IServiceSettingRepository settings,
    ICreatorChallengeRepository challenges,
    IChallengeRepository challengeEntries,
    IPlayerRepository players)
{
    /// <summary>Сколько незавершённых челленджей может вести блогер одновременно.</summary>
    public const int MaxOpenChallenges = 5;

    /// <summary>Самый длинный челлендж: дольше двух недель таблица перестаёт быть событием.</summary>
    public static readonly TimeSpan MaxChallengeLength = TimeSpan.FromDays(14);

    private static string KeyOf(long userId) => $"studio.key.{userId}";
    private static string OwnerOf(string key) => $"studio.by.{key}";

    public record StudioTournament(TournamentSummaryDto Tournament, string OverlayKey);
    public record StudioChallenge(string Code, string Title, string? Prize, DateTime StartUtc, DateTime EndUtc,
        string Status, int Participants, string Rule);
    public record StudioDto(string OverlayKey, List<StudioTournament> Tournaments, List<StudioChallenge> Challenges);

    public async Task<StudioDto> GetAsync(long userId, CancellationToken ct = default)
    {
        var key = await PersonalKeyAsync(userId, ct);

        // Турниры, созданные до появления виджетов, получают ключ при первом заходе
        var mine = await tournaments.GetByCreatorAsync(userId, 30, ct);
        var issued = false;
        foreach (var t in mine.Where(t => t.OverlayKey is null))
        {
            t.OverlayKey = TournamentValidation.NewOverlayKey();
            issued = true;
        }
        if (issued) await tournaments.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        var counts = (await challengeEntries.GetEventCountsAsync(ct)).ToDictionary(x => x.EventId, x => x.Count);
        var myChallenges = (await challenges.GetByCreatorAsync(userId, ct))
            .Select(c => new StudioChallenge(c.Code, c.Title, c.Prize, c.StartUtc, c.EndUtc,
                ChallengeUseCase.FromCreator(c).Status(now), counts.GetValueOrDefault(c.EventId),
                ChallengeRules.Normalize(c.Rule)))
            .ToList();

        return new StudioDto(key,
            mine.Select(t => new StudioTournament(TournamentMapping.ToSummary(t), t.OverlayKey!)).ToList(),
            myChallenges);
    }

    public enum ChallengeError { BadTitle, BadDates, TooMany, NotFound, NotYours }

    public async Task<ChallengeError?> CreateChallengeAsync(long userId, string? title, string? prize,
        DateTime startUtc, DateTime endUtc, string? rule = null, CancellationToken ct = default)
    {
        if (Validate(title, startUtc, endUtc, isNew: true) is { } bad) return bad;
        var now = DateTime.UtcNow;
        var open = (await challenges.GetByCreatorAsync(userId, ct)).Count(c => c.EndUtc > now);
        if (open >= MaxOpenChallenges) return ChallengeError.TooMany;

        var player = await players.GetByTelegramIdAsync(userId, ct);
        await challenges.AddAsync(new CreatorChallenge
        {
            Code = TournamentValidation.NewOverlayKey()[..10],
            CreatorTelegramUserId = userId,
            CreatorName = Cut(player?.Name ?? "Clanify", 64)!,
            Title = Cut(title, 60)!,
            Prize = Cut(prize, 60),
            StartUtc = Utc(startUtc),
            EndUtc = Utc(endUtc),
            CreatedUtc = now,
            Rule = ChallengeRules.Normalize(rule),
        }, ct);
        return null;
    }

    public async Task<ChallengeError?> UpdateChallengeAsync(long userId, string code, string? title, string? prize,
        DateTime startUtc, DateTime endUtc, string? rule = null, CancellationToken ct = default)
    {
        var c = await challenges.GetByCodeAsync(code, ct);
        if (c is null) return ChallengeError.NotFound;
        if (c.CreatorTelegramUserId != userId) return ChallengeError.NotYours;
        if (Validate(title, startUtc, endUtc, isNew: false) is { } bad) return bad;
        var notStarted = c.StartUtc > DateTime.UtcNow;
        c.Title = Cut(title, 60)!;
        c.Prize = Cut(prize, 60);
        c.StartUtc = Utc(startUtc);
        c.EndUtc = Utc(endUtc);
        // Формат меняется только до старта: посреди челленджа смена правил переписала бы таблицу
        if (notStarted) c.Rule = ChallengeRules.Normalize(rule);
        await challenges.SaveChangesAsync(ct);
        return null;
    }

    public async Task<ChallengeError?> DeleteChallengeAsync(long userId, string code, CancellationToken ct = default)
    {
        var c = await challenges.GetByCodeAsync(code, ct);
        if (c is null) return ChallengeError.NotFound;
        if (c.CreatorTelegramUserId != userId) return ChallengeError.NotYours;
        await challenges.DeleteAsync(c, ct);
        return null;
    }

    private static ChallengeError? Validate(string? title, DateTime start, DateTime end, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(title)) return ChallengeError.BadTitle;
        if (end <= start || end - start > MaxChallengeLength) return ChallengeError.BadDates;
        // Новый - не в прошлом; правка может трогать уже идущий
        if (isNew && Utc(end) <= DateTime.UtcNow) return ChallengeError.BadDates;
        return null;
    }

    private static DateTime Utc(DateTime d) => d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d.ToUniversalTime(), DateTimeKind.Utc);

    private static string? Cut(string? s, int max)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length > max ? s[..max] : s;
    }

    /// <summary>Новый личный ключ: старые ссылки в OBS перестают работать (если ключ утёк).</summary>
    public async Task<string> RotateAsync(long userId, CancellationToken ct = default)
    {
        if (await settings.GetAsync(KeyOf(userId), ct) is { Length: > 0 } old)
            await settings.SetAsync(OwnerOf(old), "", ct);
        var key = TournamentValidation.NewOverlayKey();
        await settings.SetAsync(KeyOf(userId), key, ct);
        await settings.SetAsync(OwnerOf(key), userId.ToString(), ct);
        return key;
    }

    /// <summary>Ключ принадлежит какому-то блогеру - виджет можно показать.</summary>
    public async Task<bool> IsValidKeyAsync(string key, CancellationToken ct = default) =>
        key.Length is >= 16 and <= 32 && !string.IsNullOrEmpty(await settings.GetAsync(OwnerOf(key), ct));

    private async Task<string> PersonalKeyAsync(long userId, CancellationToken ct)
    {
        var key = await settings.GetAsync(KeyOf(userId), ct);
        return string.IsNullOrEmpty(key) ? await RotateAsync(userId, ct) : key;
    }
}
