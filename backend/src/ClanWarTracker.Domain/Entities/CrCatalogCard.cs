namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Карта из общего справочника игры (/cards), а не из коллекции конкретного игрока.
/// Нужна там, где надо показать карту, которой у игрока ЕЩЁ НЕТ: в профиле API
/// отдаёт только открытые карты, поэтому иконку и стоимость недостающей карты
/// взять больше неоткуда.
/// </summary>
public class CrCatalogCard
{
    public required string Name { get; set; }
    public int ElixirCost { get; set; }
    public string Rarity { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string? EvoIconUrl { get; set; }

    /// <summary>&gt;0 — у карты вообще существует эволюция.</summary>
    public int MaxEvolutionLevel { get; set; }
}
