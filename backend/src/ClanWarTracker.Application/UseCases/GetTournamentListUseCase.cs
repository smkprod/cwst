using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Список турниров для вкладки "Турнир" — открытые/идущие, новые первыми.</summary>
public class GetTournamentListUseCase(ITournamentRepository tournaments)
{
    public async Task<List<TournamentSummaryDto>> ExecuteAsync(CancellationToken ct = default) =>
        (await tournaments.GetActiveAsync(ct)).Select(TournamentMapping.ToSummary).ToList();

    /// <summary>
    /// История: завершённые турниры с чемпионами.
    ///
    /// Нужна отдельной ручкой, а не флагом к списку: активные турниры открывают,
    /// чтобы играть, а прошедшие — чтобы посмотреть, кто выиграл. Смешивать их в
    /// одном списке значит топить сегодняшний турнир под вчерашними.
    /// </summary>
    public async Task<List<TournamentSummaryDto>> HistoryAsync(int limit = 20, CancellationToken ct = default) =>
        (await tournaments.GetFinishedAsync(limit, ct)).Select(TournamentMapping.ToSummary).ToList();
}
