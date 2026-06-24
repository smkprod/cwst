namespace ClanWarTracker.Application.DTOs;

public record TournamentSummaryDto(
    int Id,
    string Name,
    string Status,                 // registrationOpen | bracketReady | inProgress | completed | cancelled
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
    string ClanInviteLink,
    string CreatorName,
    int BestOf,
    int MinParticipants,
    int MaxParticipants,
    string Status,
    DateTime CreatedAtUtc,
    bool IsCreator,
    bool IsParticipant,
    bool CanJoin,
    List<TournamentParticipantDto> Participants,
    List<TournamentMatchDto> Matches);

public record TournamentParticipantDto(
    int Id,
    string PlayerTag,
    string PlayerName,
    int Seed,
    string Status,                 // active | eliminated | withdrawn
    int? FinalPlacement);

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
    int? NextMatchId);

/// <summary>Запись в истории турниров игрока — для вкладки статистики.</summary>
public record PlayerTournamentHistoryDto(
    int TournamentId,
    string TournamentName,
    string Status,
    int? FinalPlacement,
    int ParticipantCount,
    DateTime CreatedAtUtc);
