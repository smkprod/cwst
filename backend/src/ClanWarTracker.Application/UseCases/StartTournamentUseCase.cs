using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum StartTournamentError { TournamentNotFound, NotCreator, NotBracketReady }

/// <summary>
/// Явный запуск турнира создателем: сетка готова → турнир идёт. Рассылает участникам
/// уведомление в Telegram. Сбой отправки одному участнику не должен ронять запуск турнира.
/// </summary>
public class StartTournamentUseCase(ITournamentRepository tournaments, INotificationSender notifier)
{
    public async Task<StartTournamentError?> ExecuteAsync(
        int tournamentId, long telegramUserId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return StartTournamentError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId) return StartTournamentError.NotCreator;
        if (tournament.Status != TournamentStatus.BracketReady) return StartTournamentError.NotBracketReady;

        tournament.Status = TournamentStatus.InProgress;
        tournament.StartedAtUtc = DateTime.UtcNow;
        await tournaments.SaveChangesAsync(ct);

        var text = $"🏆 Турнир «{tournament.Name}» начался!\nСетка готова — заходи в приложение и следи за своим матчем.";
        foreach (var p in tournament.Participants.Where(p => p.Status == TournamentParticipantStatus.Active))
        {
            try { await notifier.SendToUserAsync(p.TelegramUserId, text, ct); }
            catch { /* участник заблокировал бота и т.п. — не должно мешать остальным */ }
        }

        return null;
    }
}
