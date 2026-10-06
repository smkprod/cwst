using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Студия» блогера: его турниры и ключи виджетов для OBS.
///
/// Виджет открывается в OBS как обычная веб-страница - без Telegram, поэтому
/// доступ к нему даёт только секрет в ссылке. У турнира секрет свой (сетка и
/// текущий матч), у блогера - личный: для общих таблиц лиги и челленджа.
/// </summary>
public class StudioUseCase(ITournamentRepository tournaments, IServiceSettingRepository settings)
{
    private static string KeyOf(long userId) => $"studio.key.{userId}";
    private static string OwnerOf(string key) => $"studio.by.{key}";

    public record StudioTournament(TournamentSummaryDto Tournament, string OverlayKey);
    public record StudioDto(string OverlayKey, List<StudioTournament> Tournaments);

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

        return new StudioDto(key, mine
            .Select(t => new StudioTournament(TournamentMapping.ToSummary(t), t.OverlayKey!))
            .ToList());
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
