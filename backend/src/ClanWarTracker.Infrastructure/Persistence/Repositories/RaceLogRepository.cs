using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class RaceLogRepository(AppDbContext db) : IRaceLogRepository
{
    public async Task<HashSet<(string ClanTag, int SeasonId, int SectionIndex)>> KnownWeeksAsync(
        IReadOnlyCollection<string> clanTags, CancellationToken ct = default)
    {
        var rows = await db.RaceLogPlayerWeeks.AsNoTracking()
            .Where(r => clanTags.Contains(r.ClanTag))
            .Select(r => new { r.ClanTag, r.SeasonId, r.SectionIndex })
            .Distinct()
            .ToListAsync(ct);
        return rows.Select(r => (r.ClanTag, r.SeasonId, r.SectionIndex)).ToHashSet();
    }

    public async Task AddRangeAsync(IReadOnlyCollection<RaceLogPlayerWeek> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return;
        db.RaceLogPlayerWeeks.AddRange(rows);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Ту же неделю записал соседний такт - не страшно, она уже есть
            foreach (var r in rows) db.Entry(r).State = EntityState.Detached;
        }
    }

    public Task<List<RaceLogPlayerWeek>> GetForPlayerAsync(string playerTag, int limit, CancellationToken ct = default) =>
        db.RaceLogPlayerWeeks.AsNoTracking()
            .Where(r => r.PlayerTag == playerTag)
            .OrderByDescending(r => r.SeasonId).ThenByDescending(r => r.SectionIndex)
            .Take(limit)
            .ToListAsync(ct);

    public Task PruneAsync(DateTime beforeUtc, CancellationToken ct = default) =>
        db.RaceLogPlayerWeeks.Where(r => r.CreatedUtc < beforeUtc).ExecuteDeleteAsync(ct);
}
