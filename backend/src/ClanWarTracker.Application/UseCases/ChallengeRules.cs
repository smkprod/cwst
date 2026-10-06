namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Форматы челленджа: за что даётся очко. Всё считается по журналу боёв -
/// ладдер и Путь легенд, как у уикенд-челленджа.
/// </summary>
public static class ChallengeRules
{
    /// <summary>Билеты: победа - 1, каждая третья победа подряд - 2. Формат уикенд-челленджа.</summary>
    public const string Tickets = "tickets";
    /// <summary>Каждая победа - очко, без бонусов: самый понятный формат.</summary>
    public const string Wins = "wins";
    /// <summary>Очко только за победу с тремя коронами.</summary>
    public const string ThreeCrowns = "threecrowns";
    /// <summary>Очко только за победу, в которой не потеряно ни одной башни.</summary>
    public const string Flawless = "flawless";
    /// <summary>Счёт - лучшая серия побед подряд за время челленджа.</summary>
    public const string Streak = "streak";
    /// <summary>Очко за победу только на Пути легенд.</summary>
    public const string PathOfLegends = "pol";

    public static readonly string[] All = [Tickets, Wins, ThreeCrowns, Flawless, Streak, PathOfLegends];

    public static string Normalize(string? rule) =>
        rule is not null && All.Contains(rule) ? rule : Tickets;
}
