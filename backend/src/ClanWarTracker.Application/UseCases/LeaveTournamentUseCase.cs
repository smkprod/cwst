using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum LeaveTournamentError { TournamentNotFound, NotJoined, AlreadyStarted }

public class LeaveTournamentUseCase(ITournamentRepository tournaments)
{
    public async Task<LeaveTournamentError?> ExecuteAsync(int tournamentId, long telegramUserId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return LeaveTournamentError.TournamentNotFound;

        var participant = tournament.Participants.FirstOrDefault(p =>
            p.TelegramUserId == telegramUserId && p.Status != TournamentParticipantStatus.Withdrawn);
        if (participant is null) return LeaveTournamentError.NotJoined;

        // После жеребьёвки выход из сетки сломает её — пока не поддерживаем.
        if (tournament.Status != TournamentStatus.RegistrationOpen) return LeaveTournamentError.AlreadyStarted;

        // Создатель не должен оставлять турнир без себя как участника, если хочет выйти из
        // сетки — выходит так же, как любой участник; редактировать турнир он сможет и не играя.
        tournament.Participants.Remove(participant);

        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
