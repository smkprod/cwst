using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class AcquisitionRepository(AppDbContext db) : IAcquisitionRepository
{
    public Task<Acquisition?> GetAsync(long telegramUserId, CancellationToken ct = default) =>
        db.Acquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId, ct);

    public async Task<bool> AddIfNewAsync(Acquisition acquisition, CancellationToken ct = default)
    {
        if (await db.Acquisitions.AnyAsync(a => a.TelegramUserId == acquisition.TelegramUserId, ct)) return false;
        db.Acquisitions.Add(acquisition);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Два /start подряд от одного человека: второй упёрся в уникальный
            // индекс. Первый источник уже записан — это ровно то, что нужно.
            db.Entry(acquisition).State = EntityState.Detached;
            return false;
        }
    }

    public async Task MarkLinkedAsync(long telegramUserId, CancellationToken ct = default)
    {
        var a = await db.Acquisitions.FirstOrDefaultAsync(x => x.TelegramUserId == telegramUserId, ct);
        if (a is null || a.LinkedAtUtc is not null) return;
        a.LinkedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkClanConnectedAsync(long telegramUserId, CancellationToken ct = default)
    {
        var a = await db.Acquisitions.FirstOrDefaultAsync(x => x.TelegramUserId == telegramUserId, ct);
        if (a is null || a.ClanConnectedAtUtc is not null) return;
        a.ClanConnectedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<List<Acquisition>> GetAllAsync(CancellationToken ct = default) =>
        db.Acquisitions.AsNoTracking().ToListAsync(ct);
}

public class CampaignRepository(AppDbContext db) : ICampaignRepository
{
    public Task<List<Campaign>> GetAllAsync(CancellationToken ct = default) =>
        db.Campaigns.AsNoTracking().OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        db.Campaigns.AnyAsync(c => c.Code == code, ct);

    public async Task AddAsync(Campaign campaign, CancellationToken ct = default)
    {
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
    }
}
