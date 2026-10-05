namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Режимы дружеского боя для дуэли. Автор вызова выбирает режим заранее, и бот
/// засчитывает только бои в нём: «сыграли тройной эликсир вместо обычного» - уже
/// не та дуэль.
///
/// Режим узнаём по gameMode из журнала боёв: по id и, на случай нового id у того же
/// режима, по имени. Список - дружеские режимы игры (справочник RoyaleAPI cr-api-data).
/// </summary>
public static class DuelModes
{
    /// <param name="Code">Одна буква - для callback кнопки, там тесно (64 байта).</param>
    public record Mode(string Key, char Code, int[] Ids, string[] Names);

    public static readonly Mode[] All =
    [
        new("classic", 'c', [72000007, 72000054], ["Friendly", "Friendly_FixedDeckOrder"]),
        new("double", 'd', [72000011], ["DoubleElixir_Friendly"]),
        new("triple", 't', [72000032], ["TripleElixir_Friendly"]),
        new("sudden", 's', [72000031], ["Overtime_Friendly"]),
        new("rampup", 'r', [72000033], ["RampUpElixir_Friendly"]),
        new("draft", 'f', [72000005, 72000013], ["DraftMode", "DraftModeInsane"]),
        new("mirror", 'm', [72000254, 72000052], ["MirrorDeck_Friendly", "MirrorDeck"]),
        new("rage", 'g', [72000071], ["Rage_Friendly"]),
        new("mega", 'x', [72000118], ["DoubleDeck_Friendly"]),
        new("classicdecks", 'k', [72000087, 72000131, 72000355], ["ClassicDecks_Friendly", "ClassicDecks2_Friendly", "ClassicDecks_Champion_Friendly"]),
        new("touchdown", 'h', [72000045, 72000050], ["Touchdown", "Touchdown_Draft"]),
    ];

    public const string Default = "classic";

    public static Mode Get(string? key) => All.FirstOrDefault(m => m.Key == key) ?? All[0];

    public static Mode? ByCode(char code) => All.FirstOrDefault(m => m.Code == code);

    /// <summary>
    /// Бой в этом режиме? Режим без id и имени (старые записи) не отсекаем: лучше
    /// засчитать, чем навсегда подвесить дуэль из-за пустого поля в ответе API.
    /// </summary>
    public static bool Matches(string? key, int? gameModeId, string? gameModeName)
    {
        if (key is null) return true;            // дуэли до выбора режима - любой дружеский бой
        if (gameModeId is null && string.IsNullOrEmpty(gameModeName)) return true;
        var m = Get(key);
        return (gameModeId is int id && m.Ids.Contains(id))
               || (gameModeName is { } n && m.Names.Contains(n, StringComparer.OrdinalIgnoreCase));
    }
}
