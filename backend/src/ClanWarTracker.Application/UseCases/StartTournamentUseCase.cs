using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum StartTournamentError { TournamentNotFound, NotCreator, NotBracketReady }

/// <summary>
/// Явный запуск турнира создателем: сетка готова → турнир идёт. Рассылает участникам
/// уведомление в Telegram. Сбой отправки одному участнику не должен ронять запуск турнира.
/// </summary>
public class StartTournamentUseCase(
    ITournamentRepository tournaments, INotificationSender notifier, TournamentNotifier notify)
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

        // И сразу — кому с кем играть. «Следи за своим матчем» отправляет человека
        // искать себя в сетке; адресное «ваш матч: X vs Y» не отправляет никуда.
        foreach (var match in tournament.Matches.Where(m => m.Status == TournamentMatchStatus.Ready))
            await notify.MatchReadyAsync(tournament, match, ct);

        // Командам с баем «ваш матч готов» не придёт — играть им не с кем. Отдельное
        // сообщение нужно, чтобы они не сидели в недоумении: в сетке на шесть команд
        // таких двое из шести.
        foreach (var bye in tournament.Matches.Where(m =>
                     m.Round == 1 && m.Status == TournamentMatchStatus.Bye && m.WinnerParticipant is not null))
            await notify.ByeAsync(tournament, bye.WinnerParticipant!, ct);

        // Табло вешаем сразу со стартом: к первому же матчу оно должно быть в закрепе.
        if (await notify.UpdateScoreboardAsync(tournament, ct))
            await tournaments.SaveChangesAsync(ct);

        return null;
    }
}
