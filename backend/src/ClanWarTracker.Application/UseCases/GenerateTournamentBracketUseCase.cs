using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum GenerateBracketError { TournamentNotFound, NotCreator, NotEnoughParticipants, AlreadyPlaying }

/// <summary>Создаёт (или пересоздаёт, пока не сыгран ни один матч) сетку турнира.</summary>
public class GenerateTournamentBracketUseCase(ITournamentRepository tournaments, TournamentBracketService bracket)
{
    public async Task<GenerateBracketError?> ExecuteAsync(int tournamentId, long telegramUserId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return GenerateBracketError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId) return GenerateBracketError.NotCreator;

        if (tournament.Matches.Count > 0 && !TournamentBracketService.CanRegenerateBracket(tournament))
            return GenerateBracketError.AlreadyPlaying;

        var activeCount = tournament.Participants.Count(p => p.Status == TournamentParticipantStatus.Active);
        if (activeCount < Math.Max(2, tournament.MinParticipants)) return GenerateBracketError.NotEnoughParticipants;

        tournament.Matches.Clear();
        bracket.GenerateBracket(tournament);

        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
