using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class CreatorChallengeRepository(AppDbContext db) : ICreatorChallengeRepository
{
    public Task<CreatorChallenge?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        db.CreatorChallenges.FirstOrDefaultAsync(c => c.Code == code, ct);

    public Task<List<CreatorChallenge>> GetByCreatorAsync(long creatorTelegramUserId, CancellationToken ct = default) =>
        db.CreatorChallenges
            .Where(c => c.CreatorTelegramUserId == creatorTelegramUserId)
            .OrderByDescending(c => c.StartUtc)
            .Take(30)
            .ToListAsync(ct);

    public Task<List<CreatorChallenge>> GetVisibleAsync(DateTime endedAfterUtc, int limit, CancellationToken ct = default) =>
        db.CreatorChallenges.AsNoTracking()
            .Where(c => c.EndUtc >= endedAfterUtc)
            .OrderBy(c => c.StartUtc)
            .Take(limit)
            .ToListAsync(ct);

    public Task<List<CreatorChallenge>> GetRunningAsync(DateTime nowUtc, TimeSpan grace, CancellationToken ct = default)
    {
        var endedAfter = nowUtc - grace;
        return db.CreatorChallenges.AsNoTracking()
            .Where(c => c.StartUtc <= nowUtc && c.EndUtc >= endedAfter)
            .ToListAsync(ct);
    }

    public async Task AddAsync(CreatorChallenge challenge, CancellationToken ct = default)
    {
        db.CreatorChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CreatorChallenge challenge, CancellationToken ct = default)
    {
        db.CreatorChallenges.Remove(challenge);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
