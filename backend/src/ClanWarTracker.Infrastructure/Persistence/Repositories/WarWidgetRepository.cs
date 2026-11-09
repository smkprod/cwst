using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class WarWidgetRepository(AppDbContext db) : IWarWidgetRepository
{
    public Task<WarWidget?> GetAsync(int clanId, string lang, CancellationToken ct = default) =>
        db.WarWidgets.FirstOrDefaultAsync(w => w.ClanId == clanId && w.Lang == lang, ct);

    public Task<List<WarWidget>> ShownSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        db.WarWidgets.Where(w => w.LastShownUtc >= sinceUtc).OrderBy(w => w.Id).ToListAsync(ct);

    public async Task AddAsync(WarWidget widget, CancellationToken ct = default) =>
        await db.WarWidgets.AddAsync(widget, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
