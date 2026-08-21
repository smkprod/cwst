using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Разбор профиля игрока для набора в клан (Pro). Отвечает на вопрос, который лидер
/// задаёт себе, глядя на кандидата: потянет ли он наш уровень?
///
/// Считаем по числу карт НА МАКСИМУМЕ, а не по среднему уровню одной колоды. Причина
/// в устройстве КВ: за день игрок проводит 4 боя, и карты между колодами не
/// переиспользуются — значит на полный военный день нужно 4 разные колоды, то есть
/// 32 прокачанные карты. Одна сильная колода при пустой остальной коллекции означает,
/// что после первого боя человек играет чем попало.
/// </summary>
public static class AnalyzePlayerService
{
    /// <summary>Карт в колоде. 4 боя за военный день → 4 колоды → 32 карты.</summary>
    private const int DeckSize = 8;
    private const int WarDecksPerDay = 4;

    /// <summary>Сколько полных колод максимального уровня нужно для каждого уровня клана.</summary>
    private const int TopDecks = 4;      // 32 карты — полный военный день на максимуме
    private const int StrongDecks = 3;   // 24 карты
    private const int MidDecks = 2;      // 16 карт

    public static PlayerAnalysisDto? Build(CrPlayerInfo info, int weeksPlayed, double avgFamePerAttack)
    {
        if (info.Cards.Count == 0) return null;   // без коллекции разбирать нечего

        var max = info.MaxCardLevel > 0 ? info.MaxCardLevel : info.Cards.Max(c => c.MaxLevel);

        // Главная метрика: карт на максимуме → сколько полных колод из них соберётся
        var maxedTotal = info.Cards.Count(c => c.Level >= max);
        var fullDecks = maxedTotal / DeckSize;

        var deck = info.CurrentDeck;
        var avgDeckLevel = deck.Count > 0
            ? Math.Round(deck.Average(c => (double)c.Level), 1)
            : 0;
        var maxedInDeck = deck.Count(c => c.Level >= max);

        // Эволюции: в сильных кланах они уже обязательны, поэтому считаем отдельно
        var evoUnlocked = info.Cards.Count(c => c.EvolutionLevel > 0);
        var evoAvailable = info.Cards.Count(c => c.MaxEvolutionLevel > 0);

        var (tier, verdict, fitsClanLevel) = fullDecks switch
        {
            >= TopDecks => ("top",
                $"Собирает {fullDecks} полных колод {max} уровня — хватает на весь военный день.",
                "Топовые кланы"),
            StrongDecks => ("strong",
                $"Собирает 3 полные колоды {max} уровня. На четвёртый бой уже пойдут карты послабее.",
                "Сильные кланы"),
            MidDecks => ("mid",
                $"Хватает на 2 полные колоды {max} уровня — половину военного дня.",
                "Средние кланы"),
            1 => ("developing",
                $"Только одна колода {max} уровня. В остальных боях будет проигрывать по картам.",
                "Развивающиеся кланы"),
            _ => ("developing",
                "Ни одной полной колоды максимального уровня.",
                "Развивающиеся кланы"),
        };

        // Винрейт считаем только когда боёв достаточно, иначе процент — случайность
        double? winRate = info.BattleCount >= 50 && info.Wins + info.Losses > 0
            ? Math.Round(info.Wins * 100.0 / (info.Wins + info.Losses), 1)
            : null;

        var notes = new List<string>
        {
            $"{maxedTotal} карт на {max} уровне из {info.Cards.Count} — это {fullDecks} " +
            $"полн{(fullDecks == 1 ? "ая колода" : "ых колод")} из {WarDecksPerDay} нужных",
        };
        if (evoAvailable > 0) notes.Add($"Эволюции: {evoUnlocked} из {evoAvailable} открыто");
        if (deck.Count > 0) notes.Add($"Текущая колода: ⌀ {avgDeckLevel}, максов {maxedInDeck}/{deck.Count}");
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
            FullDecks: fullDecks,
            DecksNeeded: WarDecksPerDay,
            EvoUnlocked: evoUnlocked,
            EvoAvailable: evoAvailable,
            WinRate: winRate,
            Notes: notes);
    }
}
