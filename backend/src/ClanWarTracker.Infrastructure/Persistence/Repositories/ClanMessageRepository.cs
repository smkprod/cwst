using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ClanMessageRepository(AppDbContext db) : IClanMessageRepository
{
    public async Task<DateTime?> GetLastSentAtAsync(int fromClanId, int toClanId, CancellationToken ct = default) =>
        await db.ClanMessages.AsNoTracking()
            .Where(m => m.FromClanId == fromClanId && m.ToClanId == toClanId)
            .Select(m => (DateTime?)m.SentAtUtc)
            .MaxAsync(ct);

    public Task<int> CountSentSinceAsync(int fromClanId, DateTime sinceUtc, CancellationToken ct = default) =>
        db.ClanMessages.AsNoTracking()
            .CountAsync(m => m.FromClanId == fromClanId && m.SentAtUtc >= sinceUtc, ct);

    public async Task AddAsync(ClanMessage message, CancellationToken ct = default) =>
        await db.ClanMessages.AddAsync(message, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
