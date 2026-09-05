using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ActivityRepository(AppDbContext db) : IActivityRepository
{
    public async Task TouchAsync(int playerId, string dayUtc, bool isAction, CancellationToken ct = default)
    {
        var row = await db.ActivityDays
            .FirstOrDefaultAsync(a => a.PlayerId == playerId && a.DayUtc == dayUtc, ct);

        if (row is null)
        {
            db.ActivityDays.Add(new ActivityDay
            {
                PlayerId = playerId,
                DayUtc = dayUtc,
                Actions = isAction ? 1 : 0,
                FirstSeenUtc = DateTime.UtcNow,
            });
        }
        else if (isAction)
        {
            row.Actions++;
        }
        else
        {
            return;   // заход уже отмечен, писать нечего
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Два запроса от одного человека пришли одновременно и оба не нашли строку —
            // уникальный индекс отбил второй. Это ровно та ситуация, ради которой он и
            // стоит: счётчик посещаемости не повод ронять запрос пользователя.
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Dictionary<string, (int Active, int Acting)>> GetDailyAsync(
        string sinceDayUtc, CancellationToken ct = default)
    {
        // Даты в формате yyyy-MM-dd сравниваются как строки в том же порядке, что и
        // как даты, — поэтому обходимся без разбора на стороне БД.
        var rows = await db.ActivityDays.AsNoTracking()
            .Where(a => string.Compare(a.DayUtc, sinceDayUtc) >= 0)
            .GroupBy(a => a.DayUtc)
            .Select(g => new
            {
                Day = g.Key,
                Active = g.Count(),
                Acting = g.Count(a => a.Actions > 0),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Day, r => (r.Active, r.Acting));
    }
}
