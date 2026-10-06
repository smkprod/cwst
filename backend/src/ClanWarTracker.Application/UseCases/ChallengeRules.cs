namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Форматы челленджа: за что даётся очко. Всё считается по журналу боёв -
/// ладдер и Путь легенд, как у уикенд-челленджа.
/// </summary>
public static class ChallengeRules
{
    /// <summary>Билеты: победа - 1, каждая третья победа подряд - 2. Формат уикенд-челленджа.</summary>
    public const string Tickets = "tickets";
    /// <summary>Очко только за победу с тремя коронами.</summary>
    public const string ThreeCrowns = "threecrowns";
    /// <summary>Очко только за победу, в которой не потеряно ни одной башни.</summary>
    public const string Flawless = "flawless";
    /// <summary>Счёт - лучшая серия побед подряд за время челленджа.</summary>
    public const string Streak = "streak";
    /// <summary>Очко за победу «тяжёлой» колодой: средний эликсир строго больше <see cref="HeavyMinAvgElixir"/>.</summary>
    public const string Heavy = "heavy";

    /// <summary>Порог «тяжёлой» колоды. Больше 7 - это почти одни семёрки и восьмёрки: челлендж на смелость.</summary>
    public const double HeavyMinAvgElixir = 7.0;

    public static readonly string[] All = [Tickets, ThreeCrowns, Flawless, Streak, Heavy];

    public static string Normalize(string? rule) =>
        rule is not null && All.Contains(rule) ? rule : Tickets;
}
