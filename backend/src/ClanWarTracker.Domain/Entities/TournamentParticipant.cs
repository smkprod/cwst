using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class TournamentParticipant
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public long TelegramUserId { get; set; }
    public required string PlayerTag { get; set; }
    public required string PlayerName { get; set; }

    /// <summary>Позиция в сетке при жеребьёвке (0-based); назначается при генерации сетки.</summary>
    public int Seed { get; set; }
    public TournamentParticipantStatus Status { get; set; } = TournamentParticipantStatus.Active;
    /// <summary>Итоговое место после завершения турнира (1 = победитель). Null пока турнир не завершён.</summary>
    public int? FinalPlacement { get; set; }

    public DateTime JoinedAtUtc { get; set; }
}
