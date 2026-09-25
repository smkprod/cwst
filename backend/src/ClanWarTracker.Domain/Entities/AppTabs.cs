namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Нижние вкладки приложения: какие вообще бывают и какие показываем по умолчанию.
///
/// Набор задаётся владельцем из панели, но список допустимых ключей — здесь:
/// вкладка, которой нет во фронте, превратилась бы в пустой экран, и заметить
/// это было бы некому, кроме того, кто на неё нажал.
/// </summary>
public static class AppTabs
{
    public const string SettingKey = "tabs";

    public const string Clan = "clan";
    public const string Me = "me";
    public const string Hall = "hall";
    public const string Tournament = "tournament";
    public const string Search = "search";
    public const string More = "more";

    /// <summary>Все вкладки, какие есть. Порядок — предлагаемый по умолчанию.</summary>
    public static readonly string[] Known = [Clan, Me, Hall, Tournament, Search, More];

    /// <summary>
    /// Что показываем, пока владелец ничего не выбрал. Пять из шести: внизу больше
    /// пяти значков на телефоне превращаются в неразличимую ленту.
    /// </summary>
    public static readonly string[] Default = [Clan, Me, Hall, Search, More];

    public static bool IsKnown(string? key) =>
        key is not null && Array.IndexOf(Known, key) >= 0;

    /// <summary>Сохранённая строка в набор вкладок. Пустая или битая — умолчание.</summary>
    public static string[] Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return Default;

        var tabs = stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsKnown)
            .Distinct()
            .ToArray();

        return tabs.Length > 0 ? tabs : Default;
    }
}
