using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum JoinTournamentError
{
    PlayerNotLinked, TournamentNotFound, NotOpen, Full, AlreadyJoined,
    /// <summary>Парный турнир: не указано название команды.</summary>
    TeamNameRequired,
    /// <summary>Парный турнир: не указан тег напарника.</summary>
    PartnerRequired,
    /// <summary>Тег напарника не найден в Clash Royale.</summary>
    PartnerNotFound,
    /// <summary>Напарник — это ты сам.</summary>
    PartnerIsSelf,
    /// <summary>Этот игрок уже заявлен в турнире другой командой.</summary>
    PartnerAlreadyPlaying,
}

/// <summary>
/// Регистрация в турнире. В одиночном — просто заявка от себя, в парном — команда:
/// название и два тега, причём заявку подаёт один человек за обоих.
///
/// Напарнику не нужен ни Telegram, ни привязка к боту: в парных турнирах вторым
/// часто зовут знакомого со стороны, и требовать от него регистрации значило бы
/// обрубить половину заявок.
/// </summary>
public class JoinTournamentUseCase(
    IPlayerRepository players,
    ITournamentRepository tournaments,
    IClashRoyaleApi crApi)
{
    public async Task<JoinTournamentError?> ExecuteAsync(
        int tournamentId, long telegramUserId,
        string? teamName = null, string? partnerTag = null, CancellationToken ct = default)
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

        string? partnerName = null;
        if (tournament.Mode == TournamentMode.Duo)
        {
            teamName = teamName?.Trim();
            if (string.IsNullOrEmpty(teamName)) return JoinTournamentError.TeamNameRequired;
            if (teamName.Length > 64) teamName = teamName[..64];

            if (string.IsNullOrWhiteSpace(partnerTag)) return JoinTournamentError.PartnerRequired;
            var normalized = LinkPlayerUseCase.Normalize(partnerTag);

            if (string.Equals(normalized, player.PlayerTag, StringComparison.OrdinalIgnoreCase))
                return JoinTournamentError.PartnerIsSelf;

            // Тег проверяем у самой игры: опечатка в теге всплыла бы в день турнира,
            // когда команда уже в сетке и менять состав поздно.
            partnerName = await crApi.GetPlayerNameAsync(normalized, ct);
            if (partnerName is null) return JoinTournamentError.PartnerNotFound;

            // Один человек — одна команда. Проверяем обе роли: он может быть уже
            // заявлен и капитаном другой команды, и напарником в ней.
            var taken = tournament.Participants
                .Where(p => p.Status != TournamentParticipantStatus.Withdrawn)
                .Any(p => string.Equals(p.PlayerTag, normalized, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(p.PartnerPlayerTag, normalized, StringComparison.OrdinalIgnoreCase));
            if (taken) return JoinTournamentError.PartnerAlreadyPlaying;

            partnerTag = normalized;
        }
        else
        {
            // В одиночном турнире команда не при чём, даже если клиент что-то прислал
            teamName = null;
            partnerTag = null;
        }

        tournament.Participants.Add(new TournamentParticipant
        {
            TournamentId = tournament.Id,
            TelegramUserId = telegramUserId,
            PlayerTag = player.PlayerTag,
            PlayerName = player.Name,
            TeamName = teamName,
            PartnerPlayerTag = partnerTag,
            PartnerPlayerName = partnerName,
            JoinedAtUtc = DateTime.UtcNow,
        });

        await tournaments.SaveChangesAsync(ct);
        return null;
    }
}
