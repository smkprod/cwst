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
        CancellationToken ct = default,
        IReadOnlyList<MetaBattle>? battles = null)
    {
        // Замена целиком: повторный сбор за тот же день не должен удваивать игры.
        await db.MetaDeckDays.Where(d => d.DayUtc == dayUtc).ExecuteDeleteAsync(ct);
        await db.MetaMatchupDays.Where(m => m.DayUtc == dayUtc).ExecuteDeleteAsync(ct);

        // Строки дней сравниваются как строки: формат yyyy-MM-dd сортируется так же, как даты.
        await db.MetaDeckDays.Where(d => string.Compare(d.DayUtc, keepFromDayUtc) < 0).ExecuteDeleteAsync(ct);
        await db.MetaMatchupDays.Where(m => string.Compare(m.DayUtc, keepFromDayUtc) < 0).ExecuteDeleteAsync(ct);
        if (battles is not null)
        {
            await db.MetaBattles.Where(b => b.DayUtc == dayUtc).ExecuteDeleteAsync(ct);
            await db.MetaBattles.Where(b => string.Compare(b.DayUtc, keepFromDayUtc) < 0).ExecuteDeleteAsync(ct);
            db.MetaBattles.AddRange(battles);
        }

        db.MetaDeckDays.AddRange(decks);
        db.MetaMatchupDays.AddRange(matchups);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            // Отцепляем только своё: Clear() снял бы и чужие отслеживаемые записи, а
            // несохранённые строки меты уронили бы запись причины сбоя следом.
            foreach (var e in db.ChangeTracker.Entries<MetaDeckDay>().ToList()) e.State = EntityState.Detached;
            foreach (var e in db.ChangeTracker.Entries<MetaMatchupDay>().ToList()) e.State = EntityState.Detached;
            foreach (var e in db.ChangeTracker.Entries<MetaBattle>().ToList()) e.State = EntityState.Detached;
        }
    }

    public Task<List<MetaDeckDay>> GetDecksSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaDeckDays.AsNoTracking()
            .Where(d => string.Compare(d.DayUtc, fromDayUtc) >= 0)
            .ToListAsync(ct);

    public Task<List<MetaMatchupDay>> GetMatchupsSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaMatchupDays.AsNoTracking()
            .Where(m => string.Compare(m.DayUtc, fromDayUtc) >= 0)
            .ToListAsync(ct);

    public Task<List<MetaDeckDay>> GetDeckTotalsSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaDeckDays.AsNoTracking()
            .Where(d => string.Compare(d.DayUtc, fromDayUtc) >= 0)
            .GroupBy(d => d.DeckKey)
            .Select(g => new MetaDeckDay
            {
                DayUtc = fromDayUtc,
                DeckKey = g.Key,
                Games = g.Sum(x => x.Games),
                Wins = g.Sum(x => x.Wins),
                Draws = g.Sum(x => x.Draws),
            })
            .ToListAsync(ct);

    public Task<List<MetaMatchupDay>> GetMatchupTotalsSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaMatchupDays.AsNoTracking()
            .Where(m => string.Compare(m.DayUtc, fromDayUtc) >= 0)
            .GroupBy(m => new { m.CardKey, m.OppCardKey })
            .Select(g => new MetaMatchupDay
            {
                DayUtc = fromDayUtc,
                CardKey = g.Key.CardKey,
                OppCardKey = g.Key.OppCardKey,
                Games = g.Sum(x => x.Games),
                Wins = g.Sum(x => x.Wins),
            })
            .ToListAsync(ct);

    public Task<List<MetaBattle>> GetBattlesSinceAsync(string fromDayUtc, CancellationToken ct = default) =>
        db.MetaBattles.AsNoTracking()
            .Where(b => string.Compare(b.DayUtc, fromDayUtc) >= 0)
            .ToListAsync(ct);

    public async Task<string?> LatestDayAsync(CancellationToken ct = default) =>
        await db.MetaDeckDays.AsNoTracking()
            .OrderByDescending(d => d.DayUtc)
            .Select(d => d.DayUtc)
            .FirstOrDefaultAsync(ct);
}
