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

    public async Task<int> MergeAsync(string fromEventId, string toEventId, CancellationToken ct = default)
    {
        if (fromEventId == toEventId) return 0;
        var from = await db.ChallengeEntries.Where(e => e.EventId == fromEventId).ToListAsync(ct);
        var to = await db.ChallengeEntries.Where(e => e.EventId == toEventId).ToListAsync(ct);
        var byUser = to.ToDictionary(e => e.TelegramUserId);
        var moved = 0;
        foreach (var e in from)
        {
            if (byUser.TryGetValue(e.TelegramUserId, out var existing))
            {
                // Вступал в обе версии - оставляем одну запись с ранним вступлением
                if (e.JoinedUtc < existing.JoinedUtc) existing.JoinedUtc = e.JoinedUtc;
                db.ChallengeEntries.Remove(e);
            }
            else
            {
                e.EventId = toEventId;
                moved++;
            }
        }
        await db.SaveChangesAsync(ct);
        foreach (var e in from.Concat(to)) db.Entry(e).State = EntityState.Detached;
        return moved;
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
