using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Список турниров для вкладки "Турнир" — открытые/идущие, новые первыми.</summary>
public class GetTournamentListUseCase(ITournamentRepository tournaments, IServiceModeratorRepository moderators)
{
    /// <summary>
    /// Турниры стримеров - первыми и с пометкой: ради них люди и приходят из трансляции,
    /// и искать «тот самый турнир» среди клановых им незачем.
    /// </summary>
    public async Task<List<TournamentSummaryDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var streamers = await StreamerIdsAsync(ct);
        return (await tournaments.GetActiveAsync(ct))
            .Select(t => TournamentMapping.ToSummary(t) with { Streamer = streamers.Contains(t.CreatorTelegramUserId) })
            .OrderByDescending(t => t.Streamer)
            .ToList();
    }

    /// <summary>
    /// История: завершённые турниры с чемпионами.
    ///
    /// Нужна отдельной ручкой, а не флагом к списку: активные турниры открывают,
    /// чтобы играть, а прошедшие — чтобы посмотреть, кто выиграл. Смешивать их в
    /// одном списке значит топить сегодняшний турнир под вчерашними.
    /// </summary>
    public async Task<List<TournamentSummaryDto>> HistoryAsync(int limit = 20, CancellationToken ct = default)
    {
        var streamers = await StreamerIdsAsync(ct);
        return (await tournaments.GetFinishedAsync(limit, ct))
            .Select(t => TournamentMapping.ToSummary(t) with { Streamer = streamers.Contains(t.CreatorTelegramUserId) })
            .ToList();
    }

    /// <summary>Telegram-id блогеров: модераторы с правом «Студия».</summary>
    private async Task<HashSet<long>> StreamerIdsAsync(CancellationToken ct) =>
        (await moderators.GetAllAsync(ct))
            .Where(m => m.TelegramUserId is not null
                        && (m.Permissions & ServicePermission.Creator) == ServicePermission.Creator)
            .Select(m => m.TelegramUserId!.Value)
            .ToHashSet();
}
