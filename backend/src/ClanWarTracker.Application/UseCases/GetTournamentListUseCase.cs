using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Список турниров для вкладки "Турнир" — открытые/идущие, новые первыми.</summary>
public class GetTournamentListUseCase(ITournamentRepository tournaments)
{
    public async Task<List<TournamentSummaryDto>> ExecuteAsync(CancellationToken ct = default) =>
        (await tournaments.GetActiveAsync(ct)).Select(TournamentMapping.ToSummary).ToList();
}
