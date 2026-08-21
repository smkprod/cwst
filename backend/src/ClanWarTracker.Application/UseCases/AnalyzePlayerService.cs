using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Разбор профиля игрока для набора в клан (Pro). Отвечает на вопрос, который лидер
/// задаёт себе, глядя на кандидата: потянет ли он наш уровень?
///
/// Главный показатель — средний уровень БОЕВОЙ колоды, а не коллекции: в войне человек
/// играет восемью картами, и именно их уровень определяет, выиграет он бой или нет.
/// Коллекция из 122 карт может быть большой, а колода при этом слабой.
/// </summary>
public static class AnalyzePlayerService
{
    /// <summary>Насколько колода близка к потолку — в этих долях от максимума.</summary>
    private const double TopTierGap = 0.5;    // почти всё выкачано
    private const double StrongGap = 2.0;
    private const double MidGap = 3.5;

    public static PlayerAnalysisDto? Build(CrPlayerInfo info, int weeksPlayed, double avgFamePerAttack)
    {
        var deck = info.CurrentDeck;
        if (deck.Count == 0) return null;   // без колоды разбирать нечего

        var max = info.MaxCardLevel > 0 ? info.MaxCardLevel : deck.Max(c => c.MaxLevel);
        var avgDeckLevel = Math.Round(deck.Average(c => (double)c.Level), 1);
        var maxedInDeck = deck.Count(c => c.Level >= max);
        var maxedTotal = info.Cards.Count(c => c.Level >= max);

        // Отставание от потолка — единая шкала, не зависящая от того, 15 сейчас максимум или 16
        var gap = max - avgDeckLevel;

        var (tier, verdict, fitsClanLevel) = gap switch
        {
            <= TopTierGap => ("top", "Колода выкачана почти полностью — потянет топ-клан.",
                              "Топовые кланы"),
            <= StrongGap => ("strong", "Крепкая колода, слабых карт почти нет.",
                              "Сильные и средние кланы"),
            <= MidGap => ("mid", "Средний уровень: в сильном клане будет отставать в боях.",
                              "Средние кланы"),
            _ => ("developing", "Карты заметно недокачаны — в войне будет проигрывать по уровням.",
                  "Развивающиеся кланы"),
        };

        // Винрейт считаем только когда боёв достаточно, иначе процент — случайность
        double? winRate = info.BattleCount >= 50 && info.Wins + info.Losses > 0
            ? Math.Round(info.Wins * 100.0 / (info.Wins + info.Losses), 1)
            : null;

        var notes = new List<string>();
        if (maxedInDeck > 0) notes.Add($"{maxedInDeck} из {deck.Count} карт колоды на максимуме");
        if (winRate is double wr)
            notes.Add(wr >= 55 ? $"Винрейт {wr}% — выше среднего" : $"Винрейт {wr}%");
        if (info.WarDayWins > 0) notes.Add($"{info.WarDayWins} побед в днях войны за карьеру");
        if (weeksPlayed > 0 && avgFamePerAttack > 0)
            notes.Add($"В наших кланах: {weeksPlayed} нед., {Math.Round(avgFamePerAttack)} медалей за атаку");

        return new PlayerAnalysisDto(
            Tier: tier,
            Verdict: verdict,
            RecommendedClanLevel: fitsClanLevel,
            AvgDeckLevel: avgDeckLevel,
            MaxCardLevel: max,
            MaxedInDeck: maxedInDeck,
            DeckSize: deck.Count,
            MaxedTotal: maxedTotal,
            CardsTotal: info.Cards.Count,
            WinRate: winRate,
            Notes: notes);
    }
}
