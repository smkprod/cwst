using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum FinishTournamentError { TournamentNotFound, NotCreator, NotInProgress }

/// <summary>
/// Досрочное/ручное завершение турнира создателем — например, если доигрывать сетку
/// до конца не нужно. Обычно турнир завершается сам после финального матча
/// (см. SetTournamentMatchResultUseCase); это явная альтернатива на случай,
/// когда создатель хочет закрыть турнир раньше.
/// </summary>
public class FinishTournamentUseCase(ITournamentRepository tournaments, INotificationSender notifier)
{
    public async Task<FinishTournamentError?> ExecuteAsync(
        int tournamentId, long telegramUserId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return FinishTournamentError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId) return FinishTournamentError.NotCreator;
        if (tournament.Status != TournamentStatus.InProgress) return FinishTournamentError.NotInProgress;

        tournament.Status = TournamentStatus.Completed;
        tournament.CompletedAtUtc = DateTime.UtcNow;
        await tournaments.SaveChangesAsync(ct);

        var text = $"🏁 Турнир «{tournament.Name}» завершён организатором.\nРезультаты — в приложении.";
        foreach (var p in tournament.Participants.Where(p => p.Status != TournamentParticipantStatus.Withdrawn))
        {
            try { await notifier.SendToUserAsync(p.TelegramUserId, text, ct); }
            catch { /* участник заблокировал бота и т.п. — не должно мешать остальным */ }
        }

        return null;
    }
}
