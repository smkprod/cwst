using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Сборка DTO турнира для API — общий код между Get/List/History use case'ами.</summary>
public static class TournamentMapping
{
    public static string ToText(TournamentStatus s) => s switch
    {
        TournamentStatus.RegistrationOpen => "registrationOpen",
        TournamentStatus.BracketReady => "bracketReady",
        TournamentStatus.InProgress => "inProgress",
        TournamentStatus.Completed => "completed",
        TournamentStatus.Cancelled => "cancelled",
        _ => "registrationOpen",
    };

    public static string ToText(TournamentParticipantStatus s) => s switch
    {
        TournamentParticipantStatus.Active => "active",
        TournamentParticipantStatus.Eliminated => "eliminated",
        TournamentParticipantStatus.Withdrawn => "withdrawn",
        _ => "active",
    };

    public static string ToText(TournamentMatchStatus s) => s switch
    {
        TournamentMatchStatus.Pending => "pending",
        TournamentMatchStatus.Ready => "ready",
        TournamentMatchStatus.Bye => "bye",
        TournamentMatchStatus.Completed => "completed",
        _ => "pending",
    };

    public static TournamentSummaryDto ToSummary(Tournament t) => new(
        t.Id, t.Name, ToText(t.Status), t.BestOf, t.MaxParticipants,
        t.Participants.Count(p => p.Status != TournamentParticipantStatus.Withdrawn),
        t.CreatorName, t.CreatedAtUtc);

    public static TournamentParticipantDto ToDto(TournamentParticipant p) => new(
        p.Id, p.PlayerTag, p.PlayerName, p.Seed, ToText(p.Status), p.FinalPlacement);

    public static TournamentDto ToDto(Tournament t, long requestingTelegramUserId)
    {
        var participants = t.Participants
            .Where(p => p.Status != TournamentParticipantStatus.Withdrawn)
            .OrderBy(p => p.Seed)
            .Select(ToDto)
            .ToList();

        var participantDtoById = t.Participants.ToDictionary(p => p.Id, ToDto);

        var matches = t.Matches
            .OrderBy(m => m.Round).ThenBy(m => m.SlotIndex)
            .Select(m => new TournamentMatchDto(
                m.Id, m.Round, m.SlotIndex,
                m.ParticipantAId.HasValue ? participantDtoById.GetValueOrDefault(m.ParticipantAId.Value) : null,
                m.ParticipantBId.HasValue ? participantDtoById.GetValueOrDefault(m.ParticipantBId.Value) : null,
                m.ScoreA, m.ScoreB,
                m.WinnerParticipantId.HasValue ? participantDtoById.GetValueOrDefault(m.WinnerParticipantId.Value) : null,
                ToText(m.Status),
                m.NextMatchId))
            .ToList();

        var isParticipant = t.Participants.Any(p =>
            p.TelegramUserId == requestingTelegramUserId && p.Status != TournamentParticipantStatus.Withdrawn);

        return new TournamentDto(
            t.Id, t.Name, t.Description, t.PrizeInfo, t.ClanInviteLink,
            t.CreatorName, t.BestOf, t.MaxParticipants,
            ToText(t.Status), t.CreatedAtUtc,
            IsCreator: t.CreatorTelegramUserId == requestingTelegramUserId,
            IsParticipant: isParticipant,
            CanJoin: t.Status == TournamentStatus.RegistrationOpen && !isParticipant
                && participants.Count < t.MaxParticipants,
            participants, matches);
    }
}
