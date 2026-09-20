using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum SetMatchResultError { TournamentNotFound, NotCreator, MatchNotFound, NotPlayable, BadScore }

/// <summary>
/// Ручной ввод (или исправление) результата матча создателем турнира.
/// При исправлении уже сыгранного матча каскадно сбрасывает все матчи, которые зависели
/// от старого победителя, — иначе в сетке останется игрок, который туда не должен попасть.
/// </summary>
public class SetTournamentMatchResultUseCase(
    ITournamentRepository tournaments, TournamentBracketService bracket, TournamentNotifier notify)
{
    public async Task<SetMatchResultError?> ExecuteAsync(
        int tournamentId, int matchId, long telegramUserId, int scoreA, int scoreB, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return SetMatchResultError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId) return SetMatchResultError.NotCreator;

        var match = tournament.Matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null) return SetMatchResultError.MatchNotFound;

        if (match.ParticipantA is null || match.ParticipantB is null
            || match.Status is TournamentMatchStatus.Bye or TournamentMatchStatus.Pending)
            return SetMatchResultError.NotPlayable;

        var winsNeeded = tournament.BestOf;
        var aWins = scoreA == winsNeeded && scoreB < winsNeeded;
        var bWins = scoreB == winsNeeded && scoreA < winsNeeded;
        if (scoreA < 0 || scoreB < 0 || (!aWins && !bWins)) return SetMatchResultError.BadScore;

        // Исправление уже введённого результата — сначала откатываем всё, что от него зависело.
        if (match.Status == TournamentMatchStatus.Completed)
            bracket.ClearDownstream(match);

        // Дальше всё делает общий код — тот же, которым закрывает матч автозачёт.
        var outcome = bracket.ApplyResult(tournament, match, scoreA, scoreB, auto: false);

        await tournaments.SaveChangesAsync(ct);

        // Рассылаем только после сохранения: сообщение «вы прошли дальше» по матчу,
        // который не записался, отозвать уже нельзя.
        await notify.MatchResultAsync(tournament, match, outcome, ct);
        if (outcome.NextReady is { } next) await notify.MatchReadyAsync(tournament, next, ct);

        return null;
    }
}
