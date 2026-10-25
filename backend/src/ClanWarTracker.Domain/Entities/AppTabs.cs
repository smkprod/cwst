namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Нижнее меню приложения: какие вкладки владелец может убрать из панели.
///
/// Меню двух режимов («Клан» и «Игрок») собирает фронт; здесь - только список того,
/// что можно спрятать. Хранится именно спрятанное, а не показанное: новая вкладка
/// сразу появляется у всех, и сохранённый когда-то набор её не съедает.
///
/// Война, профиль и «Ещё» не прячутся: без них в режиме не остаётся ни главного
/// экрана, ни настроек. Спрятанная вкладка не пропадает - она остаётся в «Ещё».
/// </summary>
public static class AppTabs
{
    public const string SettingKey = "menu.hidden";

    // Режим «Клан»
    public const string Hq = "hq";
    public const string Roster = "roster";
    public const string Rating = "rating";
    public const string Halloween = "halloween";

    // Режим «Игрок»
    public const string Decks = "decks";
    public const string Trainer = "trainer";
    public const string Play = "play";

    /// <summary>«Донат» - витрина Плюса и Клан Про, в обоих режимах.</summary>
    public const string Shop = "shop";

    /// <summary>Что можно спрятать. Остальное в меню всегда.</summary>
    public static readonly string[] Optional = [Hq, Roster, Rating, Halloween, Decks, Trainer, Play, Shop];

    public static bool IsOptional(string? key) => key is not null && Array.IndexOf(Optional, key) >= 0;

    /// <summary>Сохранённая строка в список спрятанных. Пустая или битая - не спрятано ничего.</summary>
    public static string[] ParseHidden(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsOptional)
                .Distinct()
                .ToArray();
}
