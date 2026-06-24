using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public class GetTournamentUseCase(ITournamentRepository tournaments)
{
    public async Task<TournamentDto?> ExecuteAsync(int tournamentId, long telegramUserId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        return tournament is null ? null : TournamentMapping.ToDto(tournament, telegramUserId);
    }
}
