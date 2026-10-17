using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Кто сейчас с Плюсом - для звезды у имени в рейтингах, составе и на Аллее. Список
/// держится минуту: состав клана Mini App опрашивает часто, а одна выборка по всем
/// активным подпискам дешевле, чем по запросу на каждого игрока.
/// </summary>
public static class PlusHolders
{
    private static HashSet<long> _set = [];
    private static DateTime _loadedUtc = DateTime.MinValue;

    public static async Task<HashSet<long>> GetAsync(IEntitlementRepository entitlements, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now - _loadedUtc < TimeSpan.FromMinutes(1)) return _set;
        var fresh = (await entitlements.ActiveUsersAsync(Entitlement.Skus.Plus, now, ct)).ToHashSet();
        _set = fresh;
        _loadedUtc = now;
        return fresh;
    }

    public static bool Has(HashSet<long> holders, Player? player) =>
        player?.TelegramUserId is long tg && holders.Contains(tg);
}
