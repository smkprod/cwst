namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Строка ежедневного снимка мирового топа по кубкам.
///
/// API отдаёт рейтинг только на «сейчас» и не глубже тысячи мест. Всё интересное —
/// как менялся порог входа, какие колоды набирают обороты после балансных правок —
/// существует только если снимки начали копить. Задним числом это не восстановить
/// ниоткуда, поэтому таблица дешёвая и пишется каждый день.
/// </summary>
public class TopPlayer
{
    public int Id { get; set; }

    /// <summary>Дата снимка (UTC, yyyy-MM-dd) — как в остальных суточных таблицах.</summary>
    public required string DayUtc { get; set; }

    /// <summary>Место в мировом рейтинге, 1..1000.</summary>
    public int Rank { get; set; }

    public required string PlayerTag { get; set; }
    public required string Name { get; set; }
    public string? ClanName { get; set; }

    public int Trophies { get; set; }
    public int ExpLevel { get; set; }

    /// <summary>
    /// Колода на момент снимка: восемь id карт через запятую.
    /// null — профиль не открылся (закрытый или пропавший), место всё равно пишем:
    /// порог входа считается по кубкам, и дыра в колодах его не искажает.
    /// </summary>
    public string? DeckCardIds { get; set; }
}
