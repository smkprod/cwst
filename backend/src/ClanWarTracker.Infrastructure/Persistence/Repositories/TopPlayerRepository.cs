using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class TopPlayerRepository(AppDbContext db) : ITopPlayerRepository
{
    public Task<bool> HasDayAsync(string dayUtc, CancellationToken ct = default) =>
        db.TopPlayers.AsNoTracking().AnyAsync(t => t.DayUtc == dayUtc, ct);

    public async Task ReplaceDayAsync(string dayUtc, IReadOnlyList<TopPlayer> rows, CancellationToken ct = default)
    {
        // Замена целиком, а не дописывание: повторный сбор за тот же день должен
        // давать один снимок, иначе доля карт посчитается по двум тысячам строк.
        await db.TopPlayers.Where(t => t.DayUtc == dayUtc).ExecuteDeleteAsync(ct);
        db.TopPlayers.AddRange(rows);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            // И после успеха, и после сбоя: тысяча строк снимка не нужна в контексте,
            // а несохранённые строки уронили бы запись меты и причины сбоя следом.
            foreach (var e in db.ChangeTracker.Entries<TopPlayer>().ToList()) e.State = EntityState.Detached;
        }
    }

    public Task<List<TopPlayer>> GetDayAsync(string dayUtc, CancellationToken ct = default) =>
        db.TopPlayers.AsNoTracking()
            .Where(t => t.DayUtc == dayUtc)
            .OrderBy(t => t.Rank)
            .ToListAsync(ct);

    public async Task<string?> LatestDayAsync(CancellationToken ct = default) =>
        await db.TopPlayers.AsNoTracking()
            .OrderByDescending(t => t.DayUtc)
            .Select(t => t.DayUtc)
            .FirstOrDefaultAsync(ct);

    public Task<TopPlayer?> FindAsync(string dayUtc, string playerTag, CancellationToken ct = default) =>
        db.TopPlayers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.DayUtc == dayUtc && t.PlayerTag == playerTag, ct);

    public async Task<List<string>> DaysAsync(int limit, CancellationToken ct = default) =>
        await db.TopPlayers.AsNoTracking()
            .Select(t => t.DayUtc)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(limit)
            .ToListAsync(ct);
}
