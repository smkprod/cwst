namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Челлендж, который блогер устроил для своих зрителей. Правила подсчёта - те же,
/// что у общего уикенд-челленджа (билеты за победы и серии), но таблица своя:
/// в неё попадают только вступившие по ссылке блогера.
/// </summary>
public class CreatorChallenge
{
    public int Id { get; set; }

    /// <summary>Код в ссылке вступления и в виджете OBS. Участники пишутся под событием «c-{Code}».</summary>
    public required string Code { get; set; }

    public long CreatorTelegramUserId { get; set; }
    public required string CreatorName { get; set; }

    public required string Title { get; set; }
    public string? Prize { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime CreatedUtc { get; set; }

    public string EventId => "c-" + Code;
}
