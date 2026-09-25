namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Каталог фонов, которые выбирает спонсор.
///
/// Ключи живут здесь, а не в базе: картинки лежат в сборке фронта, и строка в
/// базе, которой не соответствует файл, дала бы пустой фон вместо красивого.
/// Добавление фона — это всегда и новый файл, и новая строка тут.
///
/// Два набора, а не один общий. Игроку фон достаётся узкой полосой строки, и там
/// работают широкие сцены арены. Клану он достаётся крупным блоком на Аллее, и
/// туда идут вертикальные виды королевства — в строке от них остался бы кусок
/// неба, а на блоке клана широкая арена, наоборот, обрезается по краям.
/// </summary>
public static class SponsorBackground
{
    /// <summary>Небесный — им же оформлен подиум Аллеи по умолчанию.</summary>
    public const string Sky = "sky";

    public const string Arena = "arena";
    public const string Night = "night";
    public const string Ice = "ice";
    public const string Lava = "lava";

    public const string Kingdom = "kingdom";
    public const string Kingdom2 = "kingdom2";

    /// <summary>Фоны игрока.</summary>
    public static readonly string[] ForPlayers = [Sky, Arena, Night, Ice, Lava];

    /// <summary>Фоны клана.</summary>
    public static readonly string[] ForClans = [Kingdom, Kingdom2];

    public static bool IsPlayerBackground(string? key) =>
        key is not null && Array.IndexOf(ForPlayers, key) >= 0;

    public static bool IsClanBackground(string? key) =>
        key is not null && Array.IndexOf(ForClans, key) >= 0;

    public static bool IsKnown(string? key) =>
        IsPlayerBackground(key) || IsClanBackground(key);

    /// <summary>Ключ, если он годится игроку; иначе null.</summary>
    public static string? NormalizePlayer(string? key) => IsPlayerBackground(key) ? key : null;

    /// <summary>Ключ, если он годится клану; иначе null.</summary>
    public static string? NormalizeClan(string? key) => IsClanBackground(key) ? key : null;
}
