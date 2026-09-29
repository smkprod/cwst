using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ChallengeRepository(AppDbContext db) : IChallengeRepository
{
    public Task<List<ChallengeEntry>> GetEntriesAsync(string eventId, CancellationToken ct = default) =>
        db.ChallengeEntries.AsNoTracking().Where(e => e.EventId == eventId).ToListAsync(ct);

    public Task<ChallengeEntry?> GetEntryAsync(string eventId, long telegramUserId, CancellationToken ct = default) =>
        db.ChallengeEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.TelegramUserId == telegramUserId, ct);

    public async Task<List<(string EventId, int Count, DateTime LastJoinedUtc)>> GetEventCountsAsync(CancellationToken ct = default)
    {
        var rows = await db.ChallengeEntries.AsNoTracking()
            .GroupBy(e => e.EventId)
            .Select(g => new { g.Key, Count = g.Count(), Last = g.Max(x => x.JoinedUtc) })
            .ToListAsync(ct);
        return rows.Select(r => (r.Key, r.Count, r.Last)).ToList();
    }

    public async Task AddAsync(ChallengeEntry entry, CancellationToken ct = default)
    {
        db.ChallengeEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Двойное нажатие «Присоединиться»: уникальный индекс уже держит первую запись.
        }
        finally
        {
            db.Entry(entry).State = EntityState.Detached;
        }
    }
}
