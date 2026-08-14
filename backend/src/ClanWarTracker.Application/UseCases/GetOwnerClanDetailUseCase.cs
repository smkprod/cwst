using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Детали клана для панели владельца: кто привязан, у кого какой @username и кто из них
/// лидер/соруководитель — то есть с кем говорить про Pro. Роли берём из CR API (кэш 5 мин),
/// поэтому дёргаем только при открытии конкретного клана, а не для всего списка.
/// </summary>
public class GetOwnerClanDetailUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    IClashRoyaleApi crApi)
{
    public async Task<OwnerClanDetailDto?> ExecuteAsync(int clanId, CancellationToken ct = default)
    {
        var clan = await clans.GetByIdAsync(clanId, ct);
        if (clan is null) return null;

        var linked = await players.GetByClanIdAsync(clanId, ct);

        // Роли — не критичны: если CR API недоступен, покажем список без пометок «лидер».
        Dictionary<string, string> roles;
        try { roles = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
        catch { roles = new(StringComparer.OrdinalIgnoreCase); }

        var lastSeen = await snapshots.GetLastCapturedByClanAsync(ct);
        lastSeen.TryGetValue(clanId, out var seen);

        var members = linked
            .Select(p =>
            {
                var role = roles.GetValueOrDefault(p.PlayerTag);
                return new OwnerMemberDto(
                    PlayerTag: p.PlayerTag,
                    Name: p.Name,
                    TelegramUsername: p.TelegramUsername,
                    TelegramUserId: p.TelegramUserId,
                    Role: role,
                    IsLeader: role is "leader" or "coLeader",
                    LinkedAtUtc: p.CreatedAtUtc);
            })
            // Главы первыми — это те, с кем имеет смысл связываться
            .OrderByDescending(m => m.IsLeader)
            .ThenByDescending(m => m.Role == "elder")
            .ThenBy(m => m.Name)
            .ToList();

        return new OwnerClanDetailDto(
            Id: clan.Id,
            ClanTag: clan.ClanTag,
            Name: clan.Name,
            Plan: clan.EffectivePlan(DateTime.UtcNow) == PlanTier.Pro ? "pro" : "free",
            PlanExpiresAtUtc: clan.PlanExpiresAtUtc,
            TelegramChatId: clan.TelegramChatId,
            TelegramMessageThreadId: clan.TelegramMessageThreadId,
            CreatedAtUtc: clan.CreatedAtUtc,
            LastActivityUtc: seen == default ? null : seen,
            ClanMemberCount: roles.Count,
            Members: members);
    }
}
