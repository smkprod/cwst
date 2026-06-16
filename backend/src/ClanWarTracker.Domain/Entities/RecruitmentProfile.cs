namespace ClanWarTracker.Domain.Entities;

public class RecruitmentProfile
{
    public int Id { get; set; }
    public required string PlayerTag { get; set; }   // #ABC123
    public long TelegramUserId { get; set; }
    public required string Name { get; set; }
    public string? Note { get; set; }                // optional "about me"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
