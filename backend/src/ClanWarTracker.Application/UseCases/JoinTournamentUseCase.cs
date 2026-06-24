using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum JoinTournamentError { PlayerNotLinked, TournamentNotFound, NotOpen, Full, AlreadyJoined }

public class JoinTournamentUseCase(IPlayerRepository players, ITournamentRepository tournaments)
{
    public async Task<JoinTournamentError?> ExecuteAsync(int tournamentId, long telegramUserId, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return JoinTournamentError.PlayerNotLinked;

        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return JoinTournamentError.TournamentNotFound;

        if (tournament.Status != TournamentStatus.RegistrationOpen) return JoinTournamentError.NotOpen;

        if (tournament.Participants.Any(p => p.TelegramUserId == telegramUserId
                && p.Status != TournamentParticipantStatus.Withdrawn))
            return JoinTournamentError.AlreadyJoined;

        var activeCount = tournament.Participants.Count(p => p.Status != TournamentParticipantStatus.Withdrawn);
        if (activeCount >= tournament.MaxParticipants) return JoinTournamentError.Full;

        tournament.Participants.Add(new TournamentParticipant
        {
            TournamentId = tournament.Id,
            TelegramUserId = telegramUserId,
            PlayerTag = player.PlayerTag,
            PlayerName = player.Name,
            JoinedAtUtc = DateTime.UtcNow,
        });

        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
