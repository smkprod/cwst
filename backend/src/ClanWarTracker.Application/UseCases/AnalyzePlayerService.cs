using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Разбор профиля игрока: в клан какой лиги ему идти прямо сейчас.
///
/// Считаем не по одной колоде, а по военному дню целиком: за день игрок проводит
/// 4 боя, карты между колодами не переиспользуются — значит нужно 4 разные колоды,
/// то есть 32 карты. Отсюда две метрики:
///   • боевой уровень — средний уровень 32 лучших карт (с чем человек реально идёт в день);
///   • полные колоды — сколько из этих 32 карт уже на потолке.
///
/// ВАЖНО: результат — это рекомендация лиги, а не приговор. Ни один ответ этого
/// разбора не должен читаться как «тебя не возьмут»: клан найдётся на любом уровне,
/// вопрос только в том, какой. Поэтому здесь нет ветки «не подходит никуда», а все
/// формулировки живут во фронтенде и переводятся на язык интерфейса.
/// </summary>
public static class AnalyzePlayerService
{
    /// <summary>Карт в колоде. 4 боя за военный день → 4 колоды → 32 карты.</summary>
    private const int DeckSize = 8;
    private const int WarDecksPerDay = 4;
    private const int WarCards = DeckSize * WarDecksPerDay;   // 32

    /// <summary>Боевой уровень, с которого начинается серебряная и золотая лига.</summary>
    private const double SilverWarLevel = 12;
    private const double GoldWarLevel = 14;

    public static PlayerAnalysisDto? Build(CrPlayerInfo info, int weeksPlayed, double avgFamePerAttack)
    {
        if (info.Cards.Count == 0) return null;   // без коллекции разбирать нечего

        var max = info.MaxCardLevel > 0 ? info.MaxCardLevel : info.Cards.Max(c => c.MaxLevel);

        // Боевой уровень: 32 лучшие карты, но делим всегда на 32. Если карт меньше,
        // четвёртую колоду собирать не из чего — среднее по неполному набору
        // рисовало бы силу, которой у игрока нет.
        var best = info.Cards.OrderByDescending(c => c.Level).Take(WarCards).ToList();
        var warLevel = Math.Round(best.Sum(c => c.Level) / (double)WarCards, 1);

        var maxedTotal = info.Cards.Count(c => c.Level >= max);
        var fullDecks = Math.Min(maxedTotal / DeckSize, WarDecksPerDay);

        var deck = info.CurrentDeck;
        var avgDeckLevel = deck.Count > 0 ? Math.Round(deck.Average(c => (double)c.Level), 1) : 0;
        var maxedInDeck = deck.Count(c => c.Level >= max);

        // Эволюции: в сильных кланах они уже обязательны, поэтому считаем отдельно
        var evoUnlocked = info.Cards.Count(c => c.EvolutionLevel > 0);
        var evoAvailable = info.Cards.Count(c => c.MaxEvolutionLevel > 0);

        // Легендарная лига — там, где на весь военный день нужны максимальные карты.
        // Ниже планка задаётся боевым уровнем: он растёт плавно и не обнуляет игрока,
        // у которого просто ещё нет карт на самом потолке.
        var (league, nextLeague, nextWarLevel, nextMaxedCards) = maxedTotal switch
        {
            >= WarCards => ("legendary", (string?)null, (double?)null, (int?)null),
            _ when warLevel >= GoldWarLevel => ("gold", "legendary", null, WarCards - maxedTotal),
            _ when warLevel >= SilverWarLevel => ("silver", "gold", GoldWarLevel, null),
            _ => ("bronze", "silver", SilverWarLevel, null),
        };

        // Винрейт считаем только когда боёв достаточно, иначе процент — случайность
        double? winRate = info.BattleCount >= 50 && info.Wins + info.Losses > 0
            ? Math.Round(info.Wins * 100.0 / (info.Wins + info.Losses), 1)
            : null;

        return new PlayerAnalysisDto(
            League: league,
            NextLeague: nextLeague,
            WarLevel: warLevel,
            NextLeagueWarLevel: nextWarLevel,
            NextLeagueMaxedCards: nextMaxedCards,
            AvgDeckLevel: avgDeckLevel,
            MaxCardLevel: max,
            MaxedInDeck: maxedInDeck,
            DeckSize: deck.Count,
            MaxedTotal: maxedTotal,
            CardsTotal: info.Cards.Count,
            FullDecks: fullDecks,
            DecksNeeded: WarDecksPerDay,
            WarCardsNeeded: WarCards,
            EvoUnlocked: evoUnlocked,
            EvoAvailable: evoAvailable,
            WinRate: winRate,
            WarDayWins: info.WarDayWins,
            WeeksPlayed: weeksPlayed,
            AvgFamePerAttack: avgFamePerAttack);
    }
}
