using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum SetMatchResultError { TournamentNotFound, NotCreator, MatchNotFound, NotPlayable, BadScore }

/// <summary>
/// Ручной ввод (или исправление) результата матча создателем турнира.
/// При исправлении уже сыгранного матча каскадно сбрасывает все матчи, которые зависели
/// от старого победителя, — иначе в сетке останется игрок, который туда не должен попасть.
/// </summary>
public class SetTournamentMatchResultUseCase(ITournamentRepository tournaments, TournamentBracketService bracket)
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

        var winner = aWins ? match.ParticipantA! : match.ParticipantB!;
        var loser = aWins ? match.ParticipantB! : match.ParticipantA!;

        match.ScoreA = scoreA;
        match.ScoreB = scoreB;
        match.WinnerParticipantId = winner.Id;
        match.WinnerParticipant = winner;
        match.Status = TournamentMatchStatus.Completed;
        match.UpdatedAtUtc = DateTime.UtcNow;

        winner.Status = TournamentParticipantStatus.Active;
        loser.Status = TournamentParticipantStatus.Eliminated;

        if (tournament.Status == TournamentStatus.BracketReady)
            tournament.Status = TournamentStatus.InProgress;

        if (match.NextMatch is null)
        {
            // Финал сыгран — турнир завершён.
            winner.FinalPlacement = 1;
            loser.FinalPlacement = 2;
            tournament.Status = TournamentStatus.Completed;
            tournament.CompletedAtUtc = DateTime.UtcNow;
        }
        else
        {
            bracket.AdvanceWinner(match, winner);
        }

        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
