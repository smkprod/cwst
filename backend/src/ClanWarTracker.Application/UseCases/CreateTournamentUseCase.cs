using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum CreateTournamentError { PlayerNotLinked, TooManyActive, BadLink, BadName, BadFormat }

public class CreateTournamentUseCase(IPlayerRepository players, ITournamentRepository tournaments)
{
    /// <summary>Сколько турниров одновременно может вести один создатель — против спама турнирами.</summary>
    private const int MaxActivePerCreator = 3;
    private const int MinParticipants = 2;
    private const int MaxParticipantsLimit = 64;

    public async Task<(Tournament? tournament, CreateTournamentError? error)> ExecuteAsync(
        long telegramUserId, string name, string? description, string? prizeInfo,
        string clanInviteLink, int bestOf, int minParticipants, int maxParticipants, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return (null, CreateTournamentError.PlayerNotLinked);

        name = name?.Trim() ?? "";
        if (name.Length is 0 or > 80) return (null, CreateTournamentError.BadName);

        if (!TournamentValidation.IsValidClanInviteLink(clanInviteLink)) return (null, CreateTournamentError.BadLink);

        if (bestOf is < 1 or > 3) return (null, CreateTournamentError.BadFormat);
        if (maxParticipants < MinParticipants || maxParticipants > MaxParticipantsLimit)
            return (null, CreateTournamentError.BadFormat);
        // Минимум для старта: не меньше 2 и не больше лимита мест.
        if (minParticipants < MinParticipants || minParticipants > maxParticipants)
            return (null, CreateTournamentError.BadFormat);

        description = TournamentValidation.Truncate(description?.Trim(), 2000);
        prizeInfo = TournamentValidation.Truncate(prizeInfo?.Trim(), 500);

        var now = DateTime.UtcNow;
        var tournament = new Tournament
        {
            Name = name,
            Description = string.IsNullOrEmpty(description) ? null : description,
            PrizeInfo = string.IsNullOrEmpty(prizeInfo) ? null : prizeInfo,
            ClanInviteLink = clanInviteLink.Trim(),
            CreatorTelegramUserId = telegramUserId,
            CreatorPlayerTag = player.PlayerTag,
            CreatorName = player.Name,
            BestOf = bestOf,
            MinParticipants = minParticipants,
            MaxParticipants = maxParticipants,
            Status = TournamentStatus.RegistrationOpen,
            CreatedAtUtc = now,
        };
        // Создатель турнира — участник по умолчанию, как и предполагает сценарий.
        tournament.Participants.Add(new TournamentParticipant
        {
            TelegramUserId = telegramUserId,
            PlayerTag = player.PlayerTag,
            PlayerName = player.Name,
            JoinedAtUtc = now,
        });

        // Атомарная проверка лимита + вставка: защищает от спама турнирами даже при
        // одновременных запросах в обход UI (см. TryAddWithinActiveLimitAsync).
        var added = await tournaments.TryAddWithinActiveLimitAsync(
            tournament, telegramUserId, MaxActivePerCreator, ct);
        if (!added) return (null, CreateTournamentError.TooManyActive);
        return (tournament, null);
    }

}
