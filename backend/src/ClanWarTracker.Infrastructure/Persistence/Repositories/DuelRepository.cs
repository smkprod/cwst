using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

/// <summary>
/// Профили и дуэли читаются с отслеживанием: use case правит их на месте и
/// сохраняет одним SaveChangesAsync, как турнирные матчи.
/// </summary>
public class DuelRepository(AppDbContext db) : IDuelRepository
{
    public Task<DuelProfile?> GetProfileAsync(long telegramUserId, CancellationToken ct = default) =>
        db.DuelProfiles.FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, ct);

    public Task<DuelProfile?> GetProfileByTagAsync(string playerTag, CancellationToken ct = default) =>
        db.DuelProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerTag == playerTag, ct);

    public Task<List<DuelProfile>> GetProfilesAsync(IReadOnlyCollection<long> telegramUserIds, CancellationToken ct = default) =>
        db.DuelProfiles.Where(p => telegramUserIds.Contains(p.TelegramUserId)).ToListAsync(ct);

    public async Task AddProfileAsync(DuelProfile profile, CancellationToken ct = default)
    {
        db.DuelProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<DuelProfile>> GetTopAsync(int limit, CancellationToken ct = default) =>
        db.DuelProfiles.AsNoTracking()
            .Where(p => p.Games > 0)
            .OrderByDescending(p => p.Rating).ThenByDescending(p => p.Wins).ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(ct);

    public Task<int> CountProfilesAsync(CancellationToken ct = default) => db.DuelProfiles.CountAsync(ct);

    public async Task<int> RankOfAsync(int rating, CancellationToken ct = default) =>
        await db.DuelProfiles.CountAsync(p => p.Games > 0 && p.Rating > rating, ct) + 1;

    public async Task<bool> TryAddDuelAsync(Duel duel, CancellationToken ct = default)
    {
        db.Duels.Add(duel);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Карточку уже приняли: второй нажавший опоздал на доли секунды.
            db.Entry(duel).State = EntityState.Detached;
            return false;
        }
    }

    public Task<Duel?> GetDuelAsync(int id, CancellationToken ct = default) =>
        db.Duels.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<Duel>> GetActiveAsync(CancellationToken ct = default) =>
        db.Duels.Where(d => d.State == DuelState.Active).ToListAsync(ct);

    public Task<Duel?> GetActiveForUserAsync(long telegramUserId, CancellationToken ct = default) =>
        db.Duels.FirstOrDefaultAsync(d => d.State == DuelState.Active
            && (d.ATelegramUserId == telegramUserId || d.BTelegramUserId == telegramUserId), ct);

    public Task<List<Duel>> GetRecentForUserAsync(long telegramUserId, int limit, CancellationToken ct = default) =>
        db.Duels.AsNoTracking()
            .Where(d => d.ATelegramUserId == telegramUserId || d.BTelegramUserId == telegramUserId)
            .OrderByDescending(d => d.AcceptedUtc)
            .Take(limit)
            .ToListAsync(ct);

    public Task<List<Duel>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        db.Duels.AsNoTracking()
            .Where(d => d.State == DuelState.Finished)
            .OrderByDescending(d => d.FinishedUtc)
            .Take(limit)
            .ToListAsync(ct);

    public Task<int> CountRatedBetweenAsync(long a, long b, DateTime sinceUtc, CancellationToken ct = default) =>
        db.Duels.CountAsync(d => d.Rated && d.State == DuelState.Finished && d.AcceptedUtc >= sinceUtc
            && ((d.ATelegramUserId == a && d.BTelegramUserId == b) || (d.ATelegramUserId == b && d.BTelegramUserId == a)), ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
