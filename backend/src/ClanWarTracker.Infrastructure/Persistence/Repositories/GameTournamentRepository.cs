using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class GameTournamentRepository(AppDbContext db) : IGameTournamentRepository
{
    public Task<List<GameTournament>> GetAllAsync(CancellationToken ct) =>
        db.GameTournaments.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);

    public Task<GameTournament?> GetByIdAsync(int id, CancellationToken ct) =>
        db.GameTournaments.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<GameTournament?> GetByTagAsync(string tournamentTag, CancellationToken ct) =>
        db.GameTournaments.FirstOrDefaultAsync(t => t.TournamentTag == tournamentTag, ct);

    public async Task AddAsync(GameTournament tournament, CancellationToken ct) =>
        await db.GameTournaments.AddAsync(tournament, ct);

    public Task RemoveAsync(GameTournament tournament, CancellationToken ct)
    {
        db.GameTournaments.Remove(tournament);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
