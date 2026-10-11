using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ChannelPostRepository(AppDbContext db) : IChannelPostRepository
{
    public Task<ChannelPost?> GetAsync(int id, CancellationToken ct = default) =>
        db.ChannelPosts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<ChannelPost>> ListAsync(IReadOnlyCollection<ChannelPostState> states, int limit, CancellationToken ct = default)
    {
        var q = db.ChannelPosts.AsNoTracking();
        if (states.Count > 0) q = q.Where(p => states.Contains(p.State));
        return q.OrderByDescending(p => p.CreatedUtc).ThenByDescending(p => p.Id).Take(limit).ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(string sourceKey, CancellationToken ct = default) =>
        db.ChannelPosts.AnyAsync(p => p.SourceKey == sourceKey, ct);

    public async Task<bool> TryAddAsync(ChannelPost post, CancellationToken ct = default)
    {
        db.ChannelPosts.Add(post);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Такой SourceKey уже есть - эту новость кто-то записал раньше нас
            db.Entry(post).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
