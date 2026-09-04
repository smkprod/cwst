using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class PuzzleRepository(AppDbContext db) : IPuzzleRepository
{
    public Task<PuzzleResult?> GetAsync(int playerId, int day, CancellationToken ct = default) =>
        db.PuzzleResults.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Day == day, ct);

    public async Task SaveAsync(PuzzleResult result, CancellationToken ct = default)
    {
        if (result.Id == 0) await db.PuzzleResults.AddAsync(result, ct);
        else db.PuzzleResults.Update(result);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Считаем назад по дням, пока подряд идут угаданные. Тянем не всю историю, а окно:
    /// серия в год — приятная фантазия, а лишние строки в памяти — реальность.
    /// </summary>
    private const int StreakWindowDays = 400;

    public async Task<int> GetStreakAsync(int playerId, int day, CancellationToken ct = default)
    {
        var solvedDays = await db.PuzzleResults.AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.Solved && r.Day <= day && r.Day > day - StreakWindowDays)
            .Select(r => r.Day)
            .ToListAsync(ct);

        var set = solvedDays.ToHashSet();

        // Сегодня ещё не угадано — серия жива, пока не кончился день, поэтому начинаем
        // считать со вчера. Иначе каждое утро человек видел бы обнулённую серию и
        // решил бы, что она сгорела.
        var cursor = set.Contains(day) ? day : day - 1;

        var streak = 0;
        while (set.Contains(cursor))
        {
            streak++;
            cursor--;
        }
        return streak;
    }
}
