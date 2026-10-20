using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class FeedbackRepository(AppDbContext db) : IFeedbackRepository
{
    public Task<FeedbackAnswer?> GetAsync(long telegramUserId, CancellationToken ct = default) =>
        db.FeedbackAnswers.FirstOrDefaultAsync(f => f.TelegramUserId == telegramUserId, ct);

    public async Task AddAsync(FeedbackAnswer answer, CancellationToken ct = default)
    {
        db.FeedbackAnswers.Add(answer);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<FeedbackAnswer>> GetAllAsync(CancellationToken ct = default) =>
        db.FeedbackAnswers.AsNoTracking().OrderByDescending(f => f.UpdatedUtc).ToListAsync(ct);

    public async Task<HashSet<long>> AnsweredIdsAsync(CancellationToken ct = default) =>
        (await db.FeedbackAnswers.AsNoTracking().Select(f => f.TelegramUserId).ToListAsync(ct)).ToHashSet();

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
