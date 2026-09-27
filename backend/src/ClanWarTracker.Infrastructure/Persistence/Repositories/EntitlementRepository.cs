using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class EntitlementRepository(AppDbContext db) : IEntitlementRepository
{
    public async Task<DateTime?> UntilAsync(long telegramUserId, string sku, CancellationToken ct = default) =>
        await db.Entitlements.AsNoTracking()
            .Where(e => e.TelegramUserId == telegramUserId && e.Sku == sku && e.RevokedAtUtc == null)
            .MaxAsync(e => (DateTime?)e.UntilUtc, ct);

    public Task<Entitlement?> LatestAsync(long telegramUserId, string sku, CancellationToken ct = default) =>
        db.Entitlements.AsNoTracking()
            .Where(e => e.TelegramUserId == telegramUserId && e.Sku == sku && e.RevokedAtUtc == null)
            .OrderByDescending(e => e.UntilUtc)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HadTrialAsync(long telegramUserId, string? playerTag, string sku, CancellationToken ct = default) =>
        db.Entitlements.AsNoTracking()
            .AnyAsync(e => e.Sku == sku && e.Source == Entitlement.Sources.Trial
                           && (e.TelegramUserId == telegramUserId || (playerTag != null && e.PlayerTag == playerTag)), ct);

    public async Task AddAsync(Entitlement entitlement, CancellationToken ct = default) =>
        await db.Entitlements.AddAsync(entitlement, ct);

    public Task<List<Entitlement>> GetByChargeAsync(string chargeId, CancellationToken ct = default) =>
        db.Entitlements.Where(e => e.ChargeId == chargeId).ToListAsync(ct);

    public Task<List<long>> ActiveUsersAsync(string sku, DateTime nowUtc, CancellationToken ct = default) =>
        db.Entitlements.AsNoTracking()
            .Where(e => e.Sku == sku && e.RevokedAtUtc == null && e.UntilUtc > nowUtc)
            .Select(e => e.TelegramUserId)
            .Distinct()
            .ToListAsync(ct);

    public Task<List<Entitlement>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        db.Entitlements.AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public Task<List<Entitlement>> GetAllAsync(string sku, CancellationToken ct = default) =>
        db.Entitlements.AsNoTracking().Where(e => e.Sku == sku).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public class PlayerAlertPrefsRepository(AppDbContext db) : IPlayerAlertPrefsRepository
{
    public Task<PlayerAlertPrefs?> GetAsync(long telegramUserId, CancellationToken ct = default) =>
        db.PlayerAlertPrefs.FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, ct);

    public async Task<PlayerAlertPrefs> GetOrCreateAsync(long telegramUserId, CancellationToken ct = default)
    {
        var prefs = await GetAsync(telegramUserId, ct);
        if (prefs is not null) return prefs;

        prefs = new PlayerAlertPrefs { TelegramUserId = telegramUserId };
        await db.PlayerAlertPrefs.AddAsync(prefs, ct);
        return prefs;
    }

    public Task<List<long>> OptedInAsync(CancellationToken ct = default) =>
        db.PlayerAlertPrefs.AsNoTracking()
            .Where(p => p.TiltAlerts == true && !p.DmBlocked)
            .Select(p => p.TelegramUserId)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (db.ChangeTracker.Entries<PlayerAlertPrefs>().Any(e => e.State == EntityState.Added))
        {
            // Две вкладки одновременно завели настройки одному человеку: вторая упрётся
            // в уникальный индекс. Первая уже записала то же самое - повторять незачем.
            db.ChangeTracker.Clear();
        }
    }
}
