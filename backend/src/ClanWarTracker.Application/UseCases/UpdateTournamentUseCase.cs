using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum UpdateTournamentError { TournamentNotFound, NotCreator, BadName, BadLink, BadFormat, AlreadyStarted }

/// <summary>Редактирование описания/призовых/ссылки и формата турнира — доступно только создателю.</summary>
public class UpdateTournamentUseCase(ITournamentRepository tournaments)
{
    public async Task<UpdateTournamentError?> ExecuteAsync(
        int tournamentId, long telegramUserId, string name, string? description, string? prizeInfo,
        string clanInviteLink, int bestOf, int minParticipants, int maxParticipants, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        if (tournament is null) return UpdateTournamentError.TournamentNotFound;
        if (tournament.CreatorTelegramUserId != telegramUserId) return UpdateTournamentError.NotCreator;

        name = name?.Trim() ?? "";
        if (name.Length is 0 or > 80) return UpdateTournamentError.BadName;
        if (!TournamentValidation.IsValidClanInviteLink(clanInviteLink)) return UpdateTournamentError.BadLink;

        description = TournamentValidation.Truncate(description?.Trim(), 2000);
        prizeInfo = TournamentValidation.Truncate(prizeInfo?.Trim(), 500);

        tournament.Name = name;
        tournament.Description = string.IsNullOrEmpty(description) ? null : description;
        tournament.PrizeInfo = string.IsNullOrEmpty(prizeInfo) ? null : prizeInfo;
        tournament.ClanInviteLink = clanInviteLink.Trim();

        // Формат, минимум и лимит мест меняют сценарий запуска — после жеребьёвки уже не редактируется.
        if (bestOf != tournament.BestOf || minParticipants != tournament.MinParticipants
            || maxParticipants != tournament.MaxParticipants)
        {
            if (tournament.Status != TournamentStatus.RegistrationOpen)
                return UpdateTournamentError.AlreadyStarted;
            if (bestOf is < 1 or > 3) return UpdateTournamentError.BadFormat;
            var activeCount = tournament.Participants.Count(p => p.Status != TournamentParticipantStatus.Withdrawn);
            if (maxParticipants < Math.Max(2, activeCount) || maxParticipants > 64)
                return UpdateTournamentError.BadFormat;
            if (minParticipants < 2 || minParticipants > maxParticipants)
                return UpdateTournamentError.BadFormat;
            tournament.BestOf = bestOf;
            tournament.MinParticipants = minParticipants;
            tournament.MaxParticipants = maxParticipants;
        }

        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
