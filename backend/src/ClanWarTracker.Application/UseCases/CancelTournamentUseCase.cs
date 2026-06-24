using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum CancelTournamentError { TournamentNotFound, NotAllowed, AlreadyFinished }

public class CancelTournamentUseCase(ITournamentRepository tournaments)
{
    /// <param name="isOwner">Глобальный владелец бота может отменить любой турнир (модерация/спам).</param>
    public async Task<CancelTournamentError?> ExecuteAsync(
        int tournamentId, long telegramUserId, bool isOwner, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return CancelTournamentError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId && !isOwner) return CancelTournamentError.NotAllowed;

        if (tournament.Status is TournamentStatus.Completed or TournamentStatus.Cancelled)
            return CancelTournamentError.AlreadyFinished;

        tournament.Status = TournamentStatus.Cancelled;
        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
