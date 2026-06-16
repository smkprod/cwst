using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class RecruitmentRepository(AppDbContext db) : IRecruitmentRepository
{
    public Task<RecruitmentProfile?> GetByPlayerTagAsync(string playerTag, CancellationToken ct) =>
        db.RecruitmentProfiles.FirstOrDefaultAsync(r => r.PlayerTag == playerTag, ct);

    public Task<List<RecruitmentProfile>> GetActiveAsync(CancellationToken ct) =>
        db.RecruitmentProfiles
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ToListAsync(ct);

    public async Task UpsertAsync(RecruitmentProfile profile, CancellationToken ct)
    {
        var existing = await GetByPlayerTagAsync(profile.PlayerTag, ct);
        if (existing is null)
        {
            db.RecruitmentProfiles.Add(profile);
        }
        else
        {
            existing.Name = profile.Name;
            existing.Note = profile.Note;
            existing.IsActive = profile.IsActive;
            existing.UpdatedAtUtc = profile.UpdatedAtUtc;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
