using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>История участия игрока в турнирах Clanify — для вкладки статистики игрока.</summary>
public class GetPlayerTournamentHistoryUseCase(ITournamentRepository tournaments)
{
    public async Task<List<PlayerTournamentHistoryDto>> ExecuteAsync(string playerTag, CancellationToken ct = default)
    {
        var history = await tournaments.GetPlayerHistoryAsync(playerTag, ct);
        return history
            .Where(p => p.Tournament is not null)
            .Select(p => new PlayerTournamentHistoryDto(
                p.Tournament!.Id,
                p.Tournament.Name,
                TournamentMapping.ToText(p.Tournament.Status),
                p.FinalPlacement,
                p.Tournament.Participants.Count,
                p.Tournament.CreatedAtUtc))
            .ToList();
    }
}
