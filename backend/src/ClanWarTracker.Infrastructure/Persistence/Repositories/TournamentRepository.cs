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

    public Task<List<TournamentParticipant>> GetPlayerHistoryAsync(string playerTag, CancellationToken ct = default) =>
        db.TournamentParticipants
            .AsNoTracking()
            .Include(p => p.Tournament).ThenInclude(t => t!.Participants)
            .Where(p => p.PlayerTag == playerTag)
            .OrderByDescending(p => p.Tournament!.CreatedAtUtc)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
