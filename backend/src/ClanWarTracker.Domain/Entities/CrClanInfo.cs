namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Профиль клана из /clans/{tag}. Раньше из этого ответа мы читали только КВ-трофеи
/// и страну — остальное приходило и выбрасывалось.
/// </summary>
public class CrClanInfo
{
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>open / inviteOnly / closed — как клан принимает новичков.</summary>
    public string? Type { get; set; }

    public int ClanScore { get; set; }
    public int ClanWarTrophies { get; set; }
    public int RequiredTrophies { get; set; }
    public int MemberCount { get; set; }
    public int DonationsPerWeek { get; set; }

    public string? LocationName { get; set; }
    public bool LocationIsCountry { get; set; }
}

/// <summary>Участник клана со всем, что отдаёт /clans/{tag}/members.</summary>
public record CrClanMember(string Tag, string Name, string Role, int Trophies, int Donations);
