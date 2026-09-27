using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class SponsorPaymentRepository(AppDbContext db) : ISponsorPaymentRepository
{
    public Task<bool> ExistsAsync(string telegramChargeId, CancellationToken ct = default) =>
        db.SponsorPayments.AsNoTracking().AnyAsync(p => p.TelegramChargeId == telegramChargeId, ct);

    public async Task AddAsync(SponsorPayment payment, CancellationToken ct = default) =>
        await db.SponsorPayments.AddAsync(payment, ct);

    public Task<List<SponsorPayment>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        db.SponsorPayments.AsNoTracking()
            .OrderByDescending(p => p.PaidAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<long> TotalStarsAsync(CancellationToken ct = default) =>
        await db.SponsorPayments.AsNoTracking().SumAsync(p => (long)p.Stars, ct);

    public Task<List<SponsorPayment>> GetAllAsync(CancellationToken ct = default) =>
        db.SponsorPayments.AsNoTracking().ToListAsync(ct);
}
