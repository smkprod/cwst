namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Неделя КВ игрока из официального журнала войн (/riverracelog) - любого клана гонки,
/// не только нашего. Журнал каждого нашего клана отдаёт пофамильные итоги всех пяти
/// кланов своих гонок, и так у бота набирается история игроков и в чужих кланах:
/// глава видит, как кандидат воевал в прошлых кланах.
/// </summary>
public class RaceLogPlayerWeek
{
    public int Id { get; set; }
    public required string PlayerTag { get; set; }
    public required string ClanTag { get; set; }
    public required string ClanName { get; set; }
    public int SeasonId { get; set; }
    public int SectionIndex { get; set; }
    public bool IsColosseum { get; set; }
    public int Fame { get; set; }
    public int DecksUsed { get; set; }

    /// <summary>Медали и колоды всего клана за неделю - для «в среднем за атаку у клана».</summary>
    public int ClanFame { get; set; }
    public int ClanDecks { get; set; }

    public DateTime CreatedUtc { get; set; }
}
