using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class FunnelRepository(AppDbContext db) : IFunnelRepository
{
    public async Task AddAsync(FunnelEvent e, CancellationToken ct = default)
    {
        var exists = await db.FunnelEvents.AnyAsync(f => f.TelegramUserId == e.TelegramUserId
            && f.DayUtc == e.DayUtc && f.Product == e.Product && f.Step == e.Step && f.Place == e.Place, ct);
        if (exists) return;
        db.FunnelEvents.Add(e);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Два открытия одновременно - уникальный индекс отбил второе, так и задумано
            db.ChangeTracker.Clear();
        }
    }

    public Task<List<FunnelEvent>> GetSinceAsync(string sinceDayUtc, CancellationToken ct = default) =>
        db.FunnelEvents.AsNoTracking()
            .Where(f => string.Compare(f.DayUtc, sinceDayUtc) >= 0)
            .ToListAsync(ct);
}
