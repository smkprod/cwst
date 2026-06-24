using System.Data;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class TournamentRepository(AppDbContext db) : ITournamentRepository
{
    public Task<Tournament?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Tournaments
            .Include(t => t.Participants)
            .Include(t => t.Matches).ThenInclude(m => m.ParticipantA)
            .Include(t => t.Matches).ThenInclude(m => m.ParticipantB)
            .Include(t => t.Matches).ThenInclude(m => m.WinnerParticipant)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<Tournament>> GetActiveAsync(CancellationToken ct = default) =>
        db.Tournaments
            .Where(t => t.Status != TournamentStatus.Completed && t.Status != TournamentStatus.Cancelled)
            .Include(t => t.Participants)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(Tournament tournament, CancellationToken ct = default) =>
        await db.Tournaments.AddAsync(tournament, ct);

    public async Task<bool> TryAddWithinActiveLimitAsync(Tournament tournament, long creatorTelegramUserId,
        int maxActive, CancellationToken ct = default)
    {
        // Сериализуемая транзакция закрывает гонку "проверил лимит → вставил": при
        // одновременных запросах одного создателя БД отклонит конфликтующую транзакцию,
        // и лимит активных турниров не получится превысить скриптом в обход UI.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var activeCount = await db.Tournaments.CountAsync(
                    t => t.CreatorTelegramUserId == creatorTelegramUserId
                         && t.Status != TournamentStatus.Completed
                         && t.Status != TournamentStatus.Cancelled, ct);
                if (activeCount >= maxActive)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }

                await db.Tournaments.AddAsync(tournament, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                // Конфликт сериализации (проигравший в гонке создания) — трактуем как
                // "лимит достигнут": пользователь получит TooManyActive, а не 500.
                await tx.RollbackAsync(ct);
                return false;
            }
        });
    }

    public Task<List<TournamentParticipant>> GetPlayerHistoryAsync(string playerTag, CancellationToken ct = default) =>
        db.TournamentParticipants
            .AsNoTracking()
            .Include(p => p.Tournament).ThenInclude(t => t!.Participants)
            .Where(p => p.PlayerTag == playerTag)
            .OrderByDescending(p => p.Tournament!.CreatedAtUtc)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
