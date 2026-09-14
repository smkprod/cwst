using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class SentNotificationRepository(AppDbContext db) : ISentNotificationRepository
{
    public async Task<HashSet<string>> GetKeysAsync(string kind, DateTime sinceUtc, CancellationToken ct = default)
    {
        var keys = await db.SentNotifications.AsNoTracking()
            .Where(n => n.Kind == kind && n.SentAtUtc >= sinceUtc)
            .Select(n => n.Key)
            .ToListAsync(ct);
        return new HashSet<string>(keys, StringComparer.Ordinal);
    }

    public async Task AddAsync(string kind, string key, CancellationToken ct = default)
    {
        db.SentNotifications.Add(new SentNotification
        {
            Kind = kind,
            Key = key,
            SentAtUtc = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Ключ уже записан (уникальный индекс). Значит уведомление считается
            // отправленным — ровно то, чего мы и добивались, поэтому не ошибка.
            db.ChangeTracker.Clear();
        }
    }

    public async Task PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default) =>
        await db.SentNotifications
            // Отметки «это случилось впервые» живут вечно: их смысл ровно в том, чтобы
            // помнить дольше двух недель. Стереть их — значит объявить первым событие,
            // которое случалось уже пять раз.
            .Where(n => n.SentAtUtc < cutoffUtc && !n.Kind.StartsWith(SentNotification.OncePrefix))
            .ExecuteDeleteAsync(ct);
}
