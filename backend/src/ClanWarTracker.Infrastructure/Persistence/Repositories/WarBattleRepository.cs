using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class WarBattleRepository(AppDbContext db) : IWarBattleRepository
{
    public async Task<DateTime?> GetLastBattleTimeAsync(int clanId, string playerTag, CancellationToken ct)
    {
        var times = db.WarBattles
            .Where(b => b.ClanId == clanId && b.PlayerTag == playerTag)
            .Select(b => b.BattleTimeUtc);
        return await times.AnyAsync(ct) ? await times.MaxAsync(ct) : null;
    }

    public Task<List<WarBattle>> GetByWeekAsync(int clanId, int seasonId, int sectionIndex, CancellationToken ct) =>
        db.WarBattles
            .Where(b => b.ClanId == clanId && b.SeasonId == seasonId && b.SectionIndex == sectionIndex)
            .OrderByDescending(b => b.BattleTimeUtc)
            .ToListAsync(ct);

    public Task<List<WarBattle>> GetSinceAsync(int clanId, DateTime sinceUtc, CancellationToken ct) =>
        db.WarBattles
            .Where(b => b.ClanId == clanId && b.BattleTimeUtc >= sinceUtc)
            .OrderByDescending(b => b.BattleTimeUtc)
            .ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<WarBattle> battles, CancellationToken ct) =>
        await db.WarBattles.AddRangeAsync(battles, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
