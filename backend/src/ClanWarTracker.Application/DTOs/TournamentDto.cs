namespace ClanWarTracker.Application.DTOs;

public record TournamentSummaryDto(
    int Id,
    string Name,
    string Status,                 // registrationOpen | bracketReady | inProgress | completed | cancelled
    string Mode,                   // solo | duo
    DateTime? StartsAtUtc,         // объявленная дата начала; null — не объявлена
    int BestOf,
    int MaxParticipants,
    int ParticipantCount,
    string CreatorName,
    DateTime CreatedAtUtc);

public record TournamentDto(
    int Id,
    string Name,
    string? Description,
    string? PrizeInfo,
    string? ClanInviteLink,     // null — организатор ещё не создал клан
    string CreatorName,
    int BestOf,
    int MinParticipants,
    int MaxParticipants,
    string Status,
    string Mode,                   // solo | duo
    DateTime? StartsAtUtc,
    DateTime CreatedAtUtc,
    bool IsCreator,
    bool IsParticipant,
    bool CanJoin,
    /// <summary>Бот сам закрывает матчи по боевому логу участников.</summary>
    bool AutoResults,
    /// <summary>Объявлять результаты матчей в чат клана организатора.</summary>
    bool AnnounceResults,
    List<TournamentParticipantDto> Participants,
    List<TournamentMatchDto> Matches);

public record TournamentParticipantDto(
    int Id,
    string PlayerTag,
    string PlayerName,
    /// <summary>Название команды в парном турнире; null — одиночный.</summary>
    string? TeamName,
    string? PartnerPlayerTag,
    string? PartnerPlayerName,
    int Seed,
    string Status,                 // active | eliminated | withdrawn
    int? FinalPlacement,
    /// <summary>
    /// Это команда того, кто смотрит. В парном турнире — капитан: напарник хранится
    /// тегом, своей привязки к Telegram у него может не быть.
    /// </summary>
    bool IsMe);

public record TournamentMatchDto(
    int Id,
    int Round,
    int SlotIndex,
    TournamentParticipantDto? ParticipantA,
    TournamentParticipantDto? ParticipantB,
    int ScoreA,
    int ScoreB,
    TournamentParticipantDto? Winner,
    string Status,                 // pending | ready | bye | completed
    /// <summary>Счёт проставил бот по логу, а не организатор руками.</summary>
    bool AutoResolved,
    int? NextMatchId);

/// <summary>Запись в истории турниров игрока — для вкладки статистики.</summary>
public record PlayerTournamentHistoryDto(
    int TournamentId,
    string TournamentName,
    string Status,
    int? FinalPlacement,
    int ParticipantCount,
    DateTime CreatedAtUtc);
