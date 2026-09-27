using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class MetaRepository(AppDbContext db) : IMetaRepository
{
    public async Task ReplaceDayAsync(
        string dayUtc,
        IReadOnlyList<MetaDeckDay> decks,
        IReadOnlyList<MetaMatchupDay> matchups,
        string keepFromDayUtc,
        CancellationToken ct = default)
    {
        // Замена целиком: повторный сбор за тот же день не должен удваивать игры.
        await db.MetaDeckDays.Where(d => d.DayUtc == dayUtc).ExecuteDeleteAsync(ct);
        await db.MetaMatchupDays.Where(m => m.DayUtc == dayUtc).ExecuteDeleteAsync(ct);

        // Строки дней сравниваются как строки: формат yyyy-MM-dd сортируется так же, как даты.
        await db.MetaDeckDays.Where(d => string.Compare(d.DayUtc, keepFromDayUtc) < 0).ExecuteDeleteAsync(ct);
        await db.MetaMatchupDays.Where(m => string.Compare(m.DayUtc, keepFromDayUtc) < 0).ExecuteDeleteAsync(ct);

        db.MetaDeckDays.AddRange(decks);
        db.MetaMatchupDays.AddRange(matchups);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task<List<MetaDeckDay>> GetDecksSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaDeckDays.AsNoTracking()
            .Where(d => string.Compare(d.DayUtc, fromDayUtc) >= 0)
            .ToListAsync(ct);

    public Task<List<MetaMatchupDay>> GetMatchupsSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaMatchupDays.AsNoTracking()
            .Where(m => string.Compare(m.DayUtc, fromDayUtc) >= 0)
            .ToListAsync(ct);

    public async Task<string?> LatestDayAsync(CancellationToken ct = default) =>
        await db.MetaDeckDays.AsNoTracking()
            .OrderByDescending(d => d.DayUtc)
            .Select(d => d.DayUtc)
            .FirstOrDefaultAsync(ct);
}
