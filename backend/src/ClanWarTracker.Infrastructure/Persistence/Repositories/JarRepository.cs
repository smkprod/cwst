using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class JarRepository(AppDbContext db) : IJarRepository
{
    public Task<JarOrder?> GetOpenOrderAsync(long telegramUserId, string product, int days, DateTime sinceUtc, CancellationToken ct = default) =>
        db.JarOrders
            .Where(o => o.TelegramUserId == telegramUserId && o.Product == product && o.Days == days
                        && o.PaidUtc == null && o.CreatedUtc >= sinceUtc)
            .OrderByDescending(o => o.CreatedUtc)
            .FirstOrDefaultAsync(ct);

    public Task<JarOrder?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        db.JarOrders.Where(o => o.Code == code).OrderByDescending(o => o.CreatedUtc).FirstOrDefaultAsync(ct);

    public Task<JarOrder?> GetOrderAsync(int id, CancellationToken ct = default) =>
        db.JarOrders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<bool> CodeTakenAsync(string code, DateTime sinceUtc, CancellationToken ct = default) =>
        db.JarOrders.AnyAsync(o => o.Code == code && o.CreatedUtc >= sinceUtc, ct);

    public async Task AddOrderAsync(JarOrder order, CancellationToken ct = default)
    {
        db.JarOrders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<JarOrder>> GetOrdersSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        db.JarOrders.AsNoTracking()
            .Where(o => o.CreatedUtc >= sinceUtc || (o.PaidUtc != null && o.PaidUtc >= sinceUtc))
            .OrderByDescending(o => o.CreatedUtc)
            .Take(200)
            .ToListAsync(ct);

    public async Task<HashSet<string>> KnownTxAsync(IReadOnlyCollection<string> txIds, CancellationToken ct = default) =>
        (await db.JarIncomings.Where(i => txIds.Contains(i.MonoTxId)).Select(i => i.MonoTxId).ToListAsync(ct)).ToHashSet();

    public Task AddIncomingAsync(JarIncoming incoming, CancellationToken ct = default)
    {
        db.JarIncomings.Add(incoming);
        return Task.CompletedTask;
    }

    public Task<JarIncoming?> GetIncomingAsync(int id, CancellationToken ct = default) =>
        db.JarIncomings.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<List<JarIncoming>> GetUnmatchedSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        db.JarIncomings.AsNoTracking()
            .Where(i => i.OrderId == null && i.TimeUtc >= sinceUtc)
            .OrderByDescending(i => i.TimeUtc)
            .Take(100)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
