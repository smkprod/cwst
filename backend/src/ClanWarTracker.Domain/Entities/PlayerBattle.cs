namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Бой привязанного игрока, сохранённый для личного разбора.
///
/// API отдаёт только последние 25 боёв - это вечер игры. Выводы вроде «после двух
/// поражений ты сливаешь» или «вечером играешь хуже» требуют недель, поэтому бои
/// копим сами и держим месяц.
/// </summary>
public class PlayerBattle
{
    public int Id { get; set; }
    public required string PlayerTag { get; set; }
    public DateTime BattleTimeUtc { get; set; }

    /// <summary>Режим как его называет API: PvP, pathOfLegend и т.д.</summary>
    public required string Type { get; set; }

    /// <summary>1 - победа, 0 - ничья, -1 - поражение.</summary>
    public int Result { get; set; }

    public int CrownsFor { get; set; }
    public int CrownsAgainst { get; set; }

    /// <summary>Своя колода: ключи восьми карт по возрастанию, см. <see cref="MetaCard.Key"/>.</summary>
    public required string DeckKey { get; set; }

    /// <summary>Колода соперника в том же формате.</summary>
    public required string OppDeckKey { get; set; }

    /// <summary>Сколько эликсира утекло за бой. null - API не прислал.</summary>
    public double? ElixirLeaked { get; set; }

    public int? TrophyChange { get; set; }
}
