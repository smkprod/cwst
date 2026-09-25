using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ServiceSettingRepository(AppDbContext db) : IServiceSettingRepository
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        await db.ServiceSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var existing = await db.ServiceSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
            db.ServiceSettings.Add(new ServiceSetting
            {
                Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow,
            });
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
