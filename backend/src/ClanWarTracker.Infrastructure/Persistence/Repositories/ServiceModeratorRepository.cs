using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class ServiceModeratorRepository(AppDbContext db) : IServiceModeratorRepository
{
    public Task<List<ServiceModerator>> GetAllAsync(CancellationToken ct) =>
        db.ServiceModerators
            .AsNoTracking()
            .OrderByDescending(m => m.AddedAtUtc)
            .ToListAsync(ct);

    /// <summary>
    /// Сначала по числовому id, и только потом по юзернейму — порядок тут несущий.
    ///
    /// Запись, за которой id уже закреплён, по юзернейму находиться не должна: иначе
    /// человек, занявший освободившийся юзернейм модератора, получил бы его права.
    /// Поэтому во второй проверке стоит условие «id ещё не проставлен».
    /// </summary>
    public async Task<ServiceModerator?> FindAsync(long telegramUserId, string? username, CancellationToken ct)
    {
        var byId = await db.ServiceModerators
            .FirstOrDefaultAsync(m => m.TelegramUserId == telegramUserId, ct);
        if (byId is not null) return byId;

        if (string.IsNullOrWhiteSpace(username)) return null;
        var normalized = ServiceModerator.NormalizeUsername(username);
        if (normalized.Length == 0) return null;

        return await db.ServiceModerators
            .FirstOrDefaultAsync(m => m.TelegramUserId == null && m.TelegramUsername == normalized, ct);
    }

    public Task<ServiceModerator?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ServiceModerators.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(ServiceModerator moderator, CancellationToken ct) =>
        await db.ServiceModerators.AddAsync(moderator, ct);

    public Task RemoveAsync(ServiceModerator moderator, CancellationToken ct)
    {
        db.ServiceModerators.Remove(moderator);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
