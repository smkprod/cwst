namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Сохранённый военный бой участника клана (журнал КВ). Собирается фоново из боевого
/// лога игроков, дедуп по (PlayerTag, BattleTimeUtc). Хранит, кто и когда отыграл бой КВ
/// и его исход — данные, которых нет в снапшотах гонки.
/// </summary>
public class WarBattle
{
    public int Id { get; set; }
    public int ClanId { get; set; }
    public required string PlayerTag { get; set; }
    public required string PlayerName { get; set; }
    public DateTime BattleTimeUtc { get; set; }
    public bool Won { get; set; }
    public int CrownsFor { get; set; }
    public int CrownsAgainst { get; set; }
    public int SeasonId { get; set; }
    public int SectionIndex { get; set; }
}
