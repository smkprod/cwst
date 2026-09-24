namespace ClanWarTracker.Domain.Entities;

/// <summary>Строка мирового рейтинга: то, что отдаёт сам rankings, без похода в профиль.</summary>
public class CrRankedPlayer
{
    public int Rank { get; set; }
    public required string Tag { get; set; }
    public required string Name { get; set; }
    public int Trophies { get; set; }
    public int ExpLevel { get; set; }
    public string? ClanName { get; set; }
}

/// <summary>
/// Ответ на запрос мирового рейтинга вместе с причиной, если он пустой.
///
/// Пустой список сам по себе ничего не объясняет: так выглядит и отказ API, и
/// успешный ответ, в котором рейтинга просто нет. Разница принципиальная — первое
/// чинится повтором, второе не чинится никогда, — а снаружи они неразличимы, и
/// суточный снимок молча не собирался неделями, не оставив ни строчки в логе.
/// </summary>
/// <param name="Problem">null — рейтинг пришёл. Иначе текст для лога.</param>
public record CrGlobalRanking(List<CrRankedPlayer> Players, string? Problem)
{
    public static CrGlobalRanking Ok(List<CrRankedPlayer> players) => new(players, null);
    public static CrGlobalRanking Failed(string problem) => new([], problem);
}
