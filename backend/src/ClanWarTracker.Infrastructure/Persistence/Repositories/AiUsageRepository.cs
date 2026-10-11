using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class AiUsageRepository(AppDbContext db) : IAiUsageRepository
{
    public async Task<int> CountAsync(long telegramUserId, string feature, string day, CancellationToken ct = default) =>
        await db.AiUsages.AsNoTracking()
            .Where(u => u.TelegramUserId == telegramUserId && u.Feature == feature && u.Day == day)
            .Select(u => u.Count)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryUseAsync(long telegramUserId, string feature, string day, int limit, CancellationToken ct = default)
    {
        if (limit <= 0) return false;
        // Первая попытка за день - строка; гонка двух первых разрешается уникальным индексом
        if (!await db.AiUsages.AnyAsync(u => u.TelegramUserId == telegramUserId && u.Feature == feature && u.Day == day, ct))
        {
            var row = new AiUsage { TelegramUserId = telegramUserId, Feature = feature, Day = day, Count = 1 };
            db.AiUsages.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
            }
        }
        // Дальше - условный инкремент одним запросом: проверка лимита и запись неразделимы
        var updated = await db.AiUsages
            .Where(u => u.TelegramUserId == telegramUserId && u.Feature == feature && u.Day == day && u.Count < limit)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Count, u => u.Count + 1), ct);
        return updated > 0;
    }

    public Task RefundAsync(long telegramUserId, string feature, string day, CancellationToken ct = default) =>
        db.AiUsages
            .Where(u => u.TelegramUserId == telegramUserId && u.Feature == feature && u.Day == day && u.Count > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Count, u => u.Count - 1), ct);
}
