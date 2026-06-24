using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? PrizeInfo { get; set; }
    /// <summary>Ссылка-приглашение во временный игровой клан, созданный под турнир.</summary>
    public required string ClanInviteLink { get; set; }

    public long CreatorTelegramUserId { get; set; }
    public required string CreatorPlayerTag { get; set; }
    public required string CreatorName { get; set; }

    /// <summary>Формат матча: сколько побед нужно для выигрыша (1 = Bo1, 2 = Bo3, 3 = Bo5...).</summary>
    public int BestOf { get; set; } = 1;
    public int MaxParticipants { get; set; } = 16;

    public TournamentStatus Status { get; set; } = TournamentStatus.RegistrationOpen;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? BracketGeneratedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<TournamentParticipant> Participants { get; set; } = [];
    public List<TournamentMatch> Matches { get; set; } = [];
}
