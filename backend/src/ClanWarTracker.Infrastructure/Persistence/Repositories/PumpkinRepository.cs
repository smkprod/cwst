using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class PumpkinRepository(AppDbContext db) : IPumpkinRepository
{
    public Task<List<int>> GetFoundAsync(long telegramUserId, CancellationToken ct = default) =>
        db.PumpkinFinds.AsNoTracking()
            .Where(p => p.TelegramUserId == telegramUserId)
            .OrderBy(p => p.PumpkinId)
            .Select(p => p.PumpkinId)
            .ToListAsync(ct);

    public async Task<bool> TryAddAsync(long telegramUserId, int pumpkinId, DateTime foundUtc, CancellationToken ct = default)
    {
        if (await db.PumpkinFinds.AnyAsync(p => p.TelegramUserId == telegramUserId && p.PumpkinId == pumpkinId, ct))
            return false;
        var row = new PumpkinFind { TelegramUserId = telegramUserId, PumpkinId = pumpkinId, FoundUtc = foundUtc };
        db.PumpkinFinds.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Двойной тап: ту же тыкву записал соседний запрос
            db.Entry(row).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<List<(long TelegramUserId, DateTime FinishedUtc)>> FinishersAsync(int total, CancellationToken ct = default)
    {
        var rows = await db.PumpkinFinds.AsNoTracking()
            .GroupBy(p => p.TelegramUserId)
            .Where(g => g.Count() >= total)
            .Select(g => new { g.Key, Last = g.Max(p => p.FoundUtc) })
            .ToListAsync(ct);
        return rows.OrderBy(r => r.Last).Select(r => (r.Key, r.Last)).ToList();
    }

    public Task<int> HuntersAsync(CancellationToken ct = default) =>
        db.PumpkinFinds.Select(p => p.TelegramUserId).Distinct().CountAsync(ct);
}
