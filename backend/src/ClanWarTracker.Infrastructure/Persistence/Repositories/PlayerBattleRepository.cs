using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class PlayerBattleRepository(AppDbContext db) : IPlayerBattleRepository
{
    public async Task<int> AddNewAsync(string playerTag, IReadOnlyList<PlayerBattle> battles, CancellationToken ct = default)
    {
        if (battles.Count == 0) return 0;

        var from = battles.Min(b => b.BattleTimeUtc);
        var known = (await db.PlayerBattles.AsNoTracking()
                .Where(b => b.PlayerTag == playerTag && b.BattleTimeUtc >= from)
                .Select(b => b.BattleTimeUtc)
                .ToListAsync(ct))
            .ToHashSet();

        // Дубли внутри самой пачки тоже отсеиваем: уникальный индекс уронил бы всю вставку.
        var fresh = battles
            .Where(b => !known.Contains(b.BattleTimeUtc))
            .GroupBy(b => b.BattleTimeUtc).Select(g => g.First())
            .ToList();
        if (fresh.Count == 0) return 0;

        db.PlayerBattles.AddRange(fresh);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Гонка двух сборов одного игрока (воркер и открытый разбор): второй
            // упрётся в индекс. Бои уже записаны первым - терять нечего.
            db.ChangeTracker.Clear();
            return 0;
        }
        db.ChangeTracker.Clear();
        return fresh.Count;
    }

    public Task<List<PlayerBattle>> GetSinceAsync(string playerTag, DateTime sinceUtc, CancellationToken ct = default) =>
        db.PlayerBattles.AsNoTracking()
            .Where(b => b.PlayerTag == playerTag && b.BattleTimeUtc >= sinceUtc)
            .OrderBy(b => b.BattleTimeUtc)
            .ToListAsync(ct);

    public Task<int> PurgeOlderThanAsync(DateTime utc, CancellationToken ct = default) =>
        db.PlayerBattles.Where(b => b.BattleTimeUtc < utc).ExecuteDeleteAsync(ct);
}
