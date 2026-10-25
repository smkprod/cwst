using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class QuestRepository(AppDbContext db) : IQuestRepository
{
    public Task<QuestDay?> GetAsync(long telegramUserId, string day, CancellationToken ct = default) =>
        db.QuestDays.FirstOrDefaultAsync(q => q.TelegramUserId == telegramUserId && q.Day == day, ct);

    public async Task<QuestDay> AddAsync(QuestDay day, CancellationToken ct = default)
    {
        db.QuestDays.Add(day);
        try
        {
            await db.SaveChangesAsync(ct);
            return day;
        }
        catch (DbUpdateException)
        {
            // Тот же день завёл соседний запрос (приложение шлёт их пачкой при открытии)
            db.Entry(day).State = EntityState.Detached;
            return await GetAsync(day.TelegramUserId, day.Day, ct) ?? throw new InvalidOperationException("quest day vanished");
        }
    }

    public Task<List<QuestDay>> GetForUserSinceAsync(long telegramUserId, string fromDay, CancellationToken ct = default) =>
        db.QuestDays.AsNoTracking()
            .Where(q => q.TelegramUserId == telegramUserId && string.Compare(q.Day, fromDay) >= 0)
            .ToListAsync(ct);

    public Task<List<QuestDay>> GetForClanAsync(int clanId, IReadOnlyCollection<string> days, CancellationToken ct = default) =>
        db.QuestDays.AsNoTracking()
            .Where(q => q.ClanId == clanId && days.Contains(q.Day))
            .ToListAsync(ct);

    public Task<List<QuestDay>> GetByDaysAsync(IReadOnlyCollection<string> days, CancellationToken ct = default) =>
        db.QuestDays.Where(q => days.Contains(q.Day)).ToListAsync(ct);

    public async Task<int> RewardDaysSinceAsync(string fromDay, CancellationToken ct = default) =>
        await db.QuestDays.Where(q => string.Compare(q.Day, fromDay) >= 0).SumAsync(q => (int?)q.RewardDays, ct) ?? 0;

    public Task PruneAsync(string beforeDay, CancellationToken ct = default) =>
        db.QuestDays.Where(q => string.Compare(q.Day, beforeDay) < 0).ExecuteDeleteAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
