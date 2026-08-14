using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class RespectRepository(AppDbContext db) : IRespectRepository
{
    public Task<Respect?> GetByGiverAndDayAsync(string fromPlayerTag, string dayUtc, CancellationToken ct = default) =>
        db.Respects.AsNoTracking()
            .FirstOrDefaultAsync(r => r.FromPlayerTag == fromPlayerTag && r.DayUtc == dayUtc, ct);

    public Task<List<Respect>> GetByClanAndDayAsync(int clanId, string dayUtc, CancellationToken ct = default) =>
        db.Respects.AsNoTracking()
            .Where(r => r.ClanId == clanId && r.DayUtc == dayUtc)
            .ToListAsync(ct);

    public async Task<(int Total, int Since)> CountForPlayerAsync(string toPlayerTag, DateTime sinceUtc, CancellationToken ct = default)
    {
        var total = await db.Respects.CountAsync(r => r.ToPlayerTag == toPlayerTag, ct);
        var since = await db.Respects.CountAsync(r => r.ToPlayerTag == toPlayerTag && r.CreatedAtUtc >= sinceUtc, ct);
        return (total, since);
    }

    public Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        db.Respects.CountAsync(r => r.CreatedAtUtc >= sinceUtc, ct);

    public async Task AddAsync(Respect respect, CancellationToken ct = default) =>
        await db.Respects.AddAsync(respect, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
