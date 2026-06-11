using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Выдача/снятие тарифа кланом владельцем сервиса (панель в Mini App).</summary>
public class SetClanPlanUseCase(IClanRepository clans)
{
    /// <param name="days">Срок Pro в днях; null = бессрочно. Игнорируется для Free.</param>
    /// <returns>false — клан не найден.</returns>
    public async Task<bool> ExecuteAsync(int clanId, PlanTier tier, int? days, CancellationToken ct = default)
    {
        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == clanId);
        if (clan is null) return false;

        clan.PlanTier = tier;
        clan.PlanExpiresAtUtc = tier == PlanTier.Pro && days is not null
            ? DateTime.UtcNow.AddDays(days.Value)
            : null;

        await clans.SaveChangesAsync(ct);
        return true;
    }
}
