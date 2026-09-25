namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Каталог фонов, которые может выбрать спонсор.
///
/// Ключи живут здесь, а не в базе: картинки лежат в сборке фронта, и строка в
/// базе, которой не соответствует файл, дала бы пустой фон вместо красивого.
/// Добавление фона — это всегда и новый файл, и новая строка тут.
/// </summary>
public static class SponsorBackground
{
    /// <summary>Небесный — им же оформлен подиум Аллеи славы по умолчанию.</summary>
    public const string Sky = "sky";

    public const string Arena = "arena";
    public const string Night = "night";
    public const string Ice = "ice";

    public static readonly string[] All = [Sky, Arena, Night, Ice];

    public static bool IsKnown(string? key) =>
        key is not null && Array.IndexOf(All, key) >= 0;

    /// <summary>Ключ, если он известен; иначе null — рисовать нечем, покажем обычный фон.</summary>
    public static string? Normalize(string? key) => IsKnown(key) ? key : null;
}
