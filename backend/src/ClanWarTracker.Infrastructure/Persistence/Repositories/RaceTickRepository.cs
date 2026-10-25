using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class RaceTickRepository(AppDbContext db) : IRaceTickRepository
{
    public async Task UpsertTickAsync(RaceTick tick, CancellationToken ct = default)
    {
        var row = await db.RaceTicks.FirstOrDefaultAsync(t => t.ClanTag == tick.ClanTag && t.SlotUtc == tick.SlotUtc, ct);
        if (row is null) db.RaceTicks.Add(tick);
        else
        {
            row.Fame = tick.Fame;
            row.DecksUsedToday = tick.DecksUsedToday;
            row.ActiveToday = tick.ActiveToday;
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Окно уже записал соседний проход - это то же самое окно, второе не нужно
            db.ChangeTracker.Clear();
        }
    }

    public async Task UpsertPlayerDaysAsync(string clanTag, int seasonId, int sectionIndex, int dayIndex,
        IReadOnlyList<RaceClanPlayer> players, DateTime utcNow, CancellationToken ct = default)
    {
        var rows = (await db.RacePlayerDays
                .Where(d => d.ClanTag == clanTag && d.SeasonId == seasonId && d.SectionIndex == sectionIndex && d.DayIndex == dayIndex)
                .ToListAsync(ct))
            .GroupBy(d => d.PlayerTag)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var p in players.Where(p => p.DecksUsed > 0).DistinctBy(p => p.Tag))
        {
            var played = p.DecksUsedToday > 0;
            if (!rows.TryGetValue(p.Tag, out var row))
            {
                db.RacePlayerDays.Add(new RacePlayerDay
                {
                    ClanTag = clanTag,
                    PlayerTag = p.Tag,
                    Name = Cut(p.Name, 64),
                    SeasonId = seasonId,
                    SectionIndex = sectionIndex,
                    DayIndex = dayIndex,
                    Decks = p.DecksUsedToday,
                    WeekFame = p.Fame,
                    FirstPlayUtc = played ? utcNow : null,
                    LastPlayUtc = played ? utcNow : null,
                    UpdatedUtc = utcNow,
                });
                continue;
            }

            if (p.DecksUsedToday > row.Decks)
            {
                row.FirstPlayUtc ??= utcNow;
                row.LastPlayUtc = utcNow;
            }
            row.Decks = Math.Max(row.Decks, p.DecksUsedToday);
            row.WeekFame = Math.Max(row.WeekFame, p.Fame);
            row.Name = Cut(p.Name, 64);
            row.UpdatedUtc = utcNow;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Параллельный проход вставил те же строки - его данные не хуже, следующий тик досчитает
            db.ChangeTracker.Clear();
        }
    }

    public Task<List<RaceTick>> GetTicksAsync(IReadOnlyCollection<string> clanTags, DateTime sinceUtc, CancellationToken ct = default) =>
        db.RaceTicks.AsNoTracking()
            .Where(t => clanTags.Contains(t.ClanTag) && t.SlotUtc >= sinceUtc)
            .OrderBy(t => t.SlotUtc)
            .ToListAsync(ct);

    public Task<List<RacePlayerDay>> GetPlayerDaysAsync(IReadOnlyCollection<string> clanTags, int seasonId, int sectionIndex,
        CancellationToken ct = default) =>
        db.RacePlayerDays.AsNoTracking()
            .Where(d => clanTags.Contains(d.ClanTag) && d.SeasonId == seasonId && d.SectionIndex == sectionIndex)
            .ToListAsync(ct);

    public async Task PruneAsync(DateTime beforeUtc, CancellationToken ct = default)
    {
        await db.RaceTicks.Where(t => t.SlotUtc < beforeUtc).ExecuteDeleteAsync(ct);
        await db.RacePlayerDays.Where(d => d.UpdatedUtc < beforeUtc).ExecuteDeleteAsync(ct);
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
