namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Отслеживаемый игровой турнир: ссылка на турнир Clash Royale по тегу (+ пароль для
/// закрытых). Живые данные тянутся из CR API по TournamentTag; здесь храним только
/// то, что вводит организатор, и снимок названия на момент добавления.
/// </summary>
public class GameTournament
{
    public int Id { get; set; }
    public required string TournamentTag { get; set; }   // тег турнира в CR (#ABC123)
    public required string Name { get; set; }            // название (снимок из API при добавлении)
    public string? Password { get; set; }                // пароль для закрытых турниров (вводит организатор)
    public long CreatorTelegramUserId { get; set; }
    public required string CreatorName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
