using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum RemoveParticipantError { TournamentNotFound, NotCreator, NotFound, AlreadyPlayed }

/// <summary>
/// Организатор снимает команду с турнира.
///
/// До жеребьёвки это просто вычеркнуть строку. После — сложнее: на участника уже
/// ссылаются матчи, и удалять его нельзя, иначе сетка развалится. Поэтому помечаем
/// выбывшим, а его несыгранные матчи закрываем проходом соперника без игры.
///
/// Команду, уже сыгравшую матч, не снимаем: её результат стоит в сетке и на нём
/// построено всё дальше. Если она сыграла и ушла, соперник по следующему матчу
/// проходит дальше — это и произойдёт само.
/// </summary>
public class RemoveTournamentParticipantUseCase(
    ITournamentRepository tournaments,
    TournamentBracketService bracket,
    TournamentNotifier notify,
    INotificationSender notifier)
{
    public async Task<RemoveParticipantError?> ExecuteAsync(
        int tournamentId, long telegramUserId, int participantId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return RemoveParticipantError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId) return RemoveParticipantError.NotCreator;

        var participant = tournament.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null || participant.Status == TournamentParticipantStatus.Withdrawn)
            return RemoveParticipantError.NotFound;

        var name = participant.TeamName ?? participant.PlayerName;

        // До жеребьёвки матчей ещё нет — убираем начисто, как при самостоятельном выходе.
        if (tournament.Status == TournamentStatus.RegistrationOpen)
        {
            tournament.Participants.Remove(participant);
            await tournaments.SaveChangesAsync(ct);
            await TellRemovedAsync(participant.TelegramUserId, tournament.Name, ct);
            return null;
        }

        var mine = tournament.Matches
            .Where(m => m.ParticipantAId == participantId || m.ParticipantBId == participantId)
            .ToList();

        // Сыгранные матчи не трогаем: на их результате держится вся сетка дальше.
        if (mine.Any(m => m.Status == TournamentMatchStatus.Completed))
            return RemoveParticipantError.AlreadyPlayed;

        var advanced = new List<(Domain.Entities.TournamentParticipant Survivor, Domain.Entities.TournamentMatch Match)>();
        foreach (var match in mine)
        {
            if (bracket.VacateSlot(match, participant) is { } survivor)
                advanced.Add((survivor, match));
        }

        participant.Status = TournamentParticipantStatus.Withdrawn;
        await tournaments.SaveChangesAsync(ct);

        await TellRemovedAsync(participant.TelegramUserId, tournament.Name, ct);

        foreach (var (survivor, match) in advanced)
        {
            try
            {
                await notifier.SendToUserAsync(survivor.TelegramUserId,
                    $"⏭ «{tournament.Name}»: соперник ({name}) снят с турнира — вы проходите дальше без игры.", ct);
            }
            catch { /* заблокировал бота — остальных это не касается */ }

            // Проход мог собрать следующую пару: если да, зовём её играть.
            if (match.NextMatch is { Status: TournamentMatchStatus.Ready } next)
                await notify.MatchReadyAsync(tournament, next, ct);
        }

        return null;
    }

    private async Task TellRemovedAsync(long telegramUserId, string tournamentName, CancellationToken ct)
    {
        try
        {
            await notifier.SendToUserAsync(telegramUserId,
                $"Вас сняли с турнира «{tournamentName}». Вопросы — к организатору.", ct);
        }
        catch { /* снятие не должно падать из-за недоставленного сообщения */ }
    }
}
