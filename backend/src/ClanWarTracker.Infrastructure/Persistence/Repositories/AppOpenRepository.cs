using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class AppOpenRepository(AppDbContext db) : IAppOpenRepository
{
    public async Task AddAsync(AppOpen open, CancellationToken ct = default)
    {
        var exists = await db.AppOpens.AnyAsync(o => o.TelegramUserId == open.TelegramUserId
            && o.DayUtc == open.DayUtc && o.Source == open.Source && o.Dest == open.Dest, ct);
        if (exists) return;
        db.AppOpens.Add(open);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Два захода одновременно - уникальный индекс отбил второй, так и задумано
            db.ChangeTracker.Clear();
        }
    }

    public Task<List<AppOpen>> GetSinceAsync(string sinceDayUtc, CancellationToken ct = default) =>
        db.AppOpens.AsNoTracking()
            .Where(o => string.Compare(o.DayUtc, sinceDayUtc) >= 0)
            .ToListAsync(ct);
}
