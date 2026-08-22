using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Панель владельца: сводка по сервису и список кланов. Считает только по данным БД
/// (без обращений к CR API), поэтому открывается быстро даже при сотнях кланов.
/// Детали конкретного клана — отдельно, в GetOwnerClanDetailUseCase.
/// </summary>
public class GetOwnerDashboardUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    IRespectRepository respects)
{
    /// <summary>Клан считается живым, если снимок войны обновлялся за это окно.</summary>
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(7);

    public async Task<OwnerStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var allClans = await clans.GetAllAsync(ct);
        var allPlayers = await players.GetAllLinkedAsync(ct);
        var lastSeen = await snapshots.GetLastCapturedByClanAsync(ct);

        var week = now - ActiveWindow;
        var month = now.AddDays(-30);

        var active = allClans.Count(c => lastSeen.TryGetValue(c.Id, out var t) && t >= week);
        var proClans = allClans.Count(c => c.EffectivePlan(now) == PlanTier.Pro);

        // Рост считаем только по записям с известной датой: у подключённых до появления
        // поля даты нет, и записывать их в «новые» или в «старые» одинаково неверно.
        var clansDated = allClans.Where(c => c.CreatedAtUtc is not null).ToList();
        var usersDated = allPlayers.Where(p => p.CreatedAtUtc is not null).ToList();

        var respects7d = await respects.CountSinceAsync(week, ct);

        return new OwnerStatsDto(
            TotalClans: allClans.Count,
            ProClans: proClans,
            FreeClans: allClans.Count - proClans,
            ChatsWithBot: allClans.Count(c => c.TelegramChatId != 0),
            ActiveClans7d: active,
            SilentClans: allClans.Count - active,

            TotalLinkedUsers: allPlayers.Count,
            UsersWithClan: allPlayers.Count(p => p.ClanId.HasValue),
            UsersWithoutClan: allPlayers.Count(p => !p.ClanId.HasValue),
            UsersWithUsername: allPlayers.Count(p => !string.IsNullOrEmpty(p.TelegramUsername)),
            // Привязанный лидером человек тегается в чате, но в ЛС ему не написать,
            // пока он сам не нажмёт «Старт». Для рассылки это разные числа.
            UsersReachableByDm: allPlayers.Count(p => p.TelegramUserId.HasValue),
            InvitedUsers: allPlayers.Count(p => p.ReferrerTelegramUserId.HasValue),

            NewClans7d: clansDated.Count(c => c.CreatedAtUtc >= week),
            NewClans30d: clansDated.Count(c => c.CreatedAtUtc >= month),
            NewUsers7d: usersDated.Count(p => p.CreatedAtUtc >= week),
            NewUsers30d: usersDated.Count(p => p.CreatedAtUtc >= month),
            ClansWithKnownDate: clansDated.Count,
            UsersWithKnownDate: usersDated.Count,

            ProExpiring7d: allClans.Count(c =>
                c.EffectivePlan(now) == PlanTier.Pro &&
                c.PlanExpiresAtUtc is not null &&
                c.PlanExpiresAtUtc <= now.AddDays(7)),
            ProExpired: allClans.Count(c =>
                c.PlanTier == PlanTier.Pro &&
                c.PlanExpiresAtUtc is not null &&
                c.PlanExpiresAtUtc <= now),
            ProForever: allClans.Count(c => c.PlanTier == PlanTier.Pro && c.PlanExpiresAtUtc is null),

            Respects7d: respects7d,
            AvgLinkedPerClan: allClans.Count == 0
                ? 0
                : Math.Round((double)allPlayers.Count(p => p.ClanId.HasValue) / allClans.Count, 1));
    }

    public async Task<List<OwnerClanDto>> GetClansAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var allClans = await clans.GetAllAsync(ct);
        var allPlayers = await players.GetAllLinkedAsync(ct);
        var lastSeen = await snapshots.GetLastCapturedByClanAsync(ct);

        var linkedByClan = allPlayers
            .Where(p => p.ClanId.HasValue)
            .GroupBy(p => p.ClanId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return allClans
            .Select(c =>
            {
                var isPro = c.EffectivePlan(now) == PlanTier.Pro;
                lastSeen.TryGetValue(c.Id, out var seen);
                var last = seen == default ? (DateTime?)null : seen;

                return new OwnerClanDto(
                    Id: c.Id,
                    ClanTag: c.ClanTag,
                    Name: c.Name,
                    Plan: isPro ? "pro" : "free",
                    PlanExpiresAtUtc: c.PlanExpiresAtUtc,
                    DaysLeft: isPro && c.PlanExpiresAtUtc is DateTime exp
                        ? Math.Max(0, (int)Math.Ceiling((exp - now).TotalDays))
                        : null,
                    LinkedPlayers: linkedByClan.GetValueOrDefault(c.Id),
                    HasChat: c.TelegramChatId != 0,
                    CreatedAtUtc: c.CreatedAtUtc,
                    LastActivityUtc: last,
                    IsActive: last is not null && last >= now - ActiveWindow);
            })
            // Сначала те, кем стоит заняться: истекающий Pro, потом молчащие, потом остальные
            .OrderBy(c => c.DaysLeft ?? int.MaxValue)
            .ThenBy(c => c.IsActive)
            .ThenByDescending(c => c.LinkedPlayers)
            .ToList();
    }
}
