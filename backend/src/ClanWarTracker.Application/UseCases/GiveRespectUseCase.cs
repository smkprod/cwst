using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public record GiveRespectResult(bool Ok, string? Error, int TargetTotal);

/// <summary>
/// Дать респект 👏 согильдийцу — лёгкая социальная награда (аналог лайка), один в сутки.
/// Коммуникация → удовольствие: единственный подтверждённый путь вовлечения (Jo & Baek).
/// </summary>
public class GiveRespectUseCase(
    IPlayerRepository players,
    IClanRepository clans,
    IRespectRepository respects,
    IClashRoyaleApi crApi)
{
    public async Task<GiveRespectResult> ExecuteAsync(long telegramUserId, string toPlayerTag, CancellationToken ct = default)
    {
        var giver = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (giver is null || giver.ClanId is not int clanId)
            return new(false, "player_not_linked", 0);

        toPlayerTag = "#" + toPlayerTag.TrimStart('#').ToUpperInvariant();
        if (string.Equals(giver.PlayerTag, toPlayerTag, StringComparison.OrdinalIgnoreCase))
            return new(false, "self_respect", 0);

        // Получатель должен быть в текущем составе клана (ушедших не чествуем)
        var clanMates = await players.GetByClanIdAsync(clanId, ct);
        var toName = clanMates.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, toPlayerTag, StringComparison.OrdinalIgnoreCase))?.Name ?? toPlayerTag;
        try
        {
            var clan = await clans.GetByIdAsync(clanId, ct);
            var roles = clan is null ? [] : await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct);
            if (roles.Count > 0 && !roles.ContainsKey(toPlayerTag))
                return new(false, "not_in_clan", 0);
        }
        catch { /* CR API прилёг — не блокируем респект */ }

        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (await respects.GetByGiverAndDayAsync(giver.PlayerTag, day, ct) is not null)
            return new(false, "already_today", 0);

        await respects.AddAsync(new Respect
        {
            ClanId = clanId,
            FromPlayerTag = giver.PlayerTag,
            FromName = giver.Name,
            ToPlayerTag = toPlayerTag,
            ToName = toName,
            DayUtc = day,
            CreatedAtUtc = DateTime.UtcNow,
        }, ct);
        await respects.SaveChangesAsync(ct);

        var (total, _) = await respects.CountForPlayerAsync(toPlayerTag, DateTime.UtcNow, ct);
        return new(true, null, total);
    }
}
