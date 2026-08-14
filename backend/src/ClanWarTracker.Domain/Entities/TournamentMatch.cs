using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class TournamentMatch
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    /// <summary>Раунд сетки, 1 = первый раунд.</summary>
    public int Round { get; set; }
    /// <summary>Позиция матча внутри раунда (0-based), сверху вниз.</summary>
    public int SlotIndex { get; set; }

    public int? ParticipantAId { get; set; }
    public TournamentParticipant? ParticipantA { get; set; }
    public int? ParticipantBId { get; set; }
    public TournamentParticipant? ParticipantB { get; set; }

    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public int? WinnerParticipantId { get; set; }
    public TournamentParticipant? WinnerParticipant { get; set; }

    public TournamentMatchStatus Status { get; set; } = TournamentMatchStatus.Pending;

    /// <summary>Матч следующего раунда, куда проходит победитель этого матча. Null для финала.</summary>
    public int? NextMatchId { get; set; }
    public TournamentMatch? NextMatch { get; set; }
    /// <summary>В какой слот следующего матча встаёт победитель: 0 = A, 1 = B.</summary>
    public int NextMatchSlot { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
