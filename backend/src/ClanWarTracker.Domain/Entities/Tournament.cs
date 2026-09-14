using ClanWarTracker.Domain.Enums;

namespace ClanWarTracker.Domain.Entities;

public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? PrizeInfo { get; set; }
    /// <summary>
    /// Ссылка-приглашение во временный игровой клан, созданный под турнир.
    ///
    /// Необязательна при создании: клан обычно делают позже, ближе к дате, а
    /// требовать ссылку заранее значит заставлять организатора либо врать
    /// заглушкой, либо откладывать объявление турнира.
    /// </summary>
    public string? ClanInviteLink { get; set; }

    public long CreatorTelegramUserId { get; set; }
    public required string CreatorPlayerTag { get; set; }
    public required string CreatorName { get; set; }

    /// <summary>Формат матча: сколько побед нужно для выигрыша (1 = Bo1, 2 = Bo3, 3 = Bo5...).</summary>
    public int BestOf { get; set; } = 1;
    /// <summary>Минимум участников, с которого создатель может запустить турнир (жеребьёвку).</summary>
    public int MinParticipants { get; set; } = 2;
    public int MaxParticipants { get; set; } = 16;

    /// <summary>1×1 или 2×2. В парном участник — команда из двоих.</summary>
    public TournamentMode Mode { get; set; } = TournamentMode.Solo;

    /// <summary>
    /// Когда турнир начинается. Нужна для предварительной регистрации: люди
    /// записываются заранее, зная дату, а не гадают, когда всё стартует.
    /// null — дата не объявлена, собираемся и стартуем вручную.
    /// </summary>
    public DateTime? StartsAtUtc { get; set; }

    public TournamentStatus Status { get; set; } = TournamentStatus.RegistrationOpen;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? BracketGeneratedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<TournamentParticipant> Participants { get; set; } = [];
    public List<TournamentMatch> Matches { get; set; } = [];
}
