namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// Одна карта в подобранной колоде. Level = 0 означает «карты у игрока ещё нет»:
/// иконка и стоимость взяты из общего справочника, уровня взять неоткуда.
/// </summary>
public record DeckCardDto(
    string Name,
    int Level,                  // 0 — карта не открыта
    int MaxLevel,
    int ElixirCost,
    string IconUrl,
    bool Owned,
    bool EvoUnlocked,           // игрок открыл эволюцию этой карты
    bool HasEvo,                // у карты вообще есть эволюция
    string? EvoIconUrl);

/// <summary>Колода меты, примеренная на коллекцию конкретного игрока.</summary>
public record DeckSuggestionDto(
    string Id,
    string Name,
    string Archetype,
    string Difficulty,          // easy | medium | hard
    string Note,
    List<DeckCardDto> Cards,
    int OwnedCount,             // сколько из 8 карт открыто
    List<string> Missing,       // чего не хватает (имена карт)
    double AvgLevel,            // средний уровень открытых карт колоды
    double AvgElixir,
    int MaxedCount,             // карт на потолке уровня
    int EvoUnlocked,            // эволюций колоды открыто у игрока
    int EvoAvailable,           // сколько карт колоды вообще имеют эволюцию
    int Readiness,              // 0..100 — насколько колода готова прямо сейчас
    string Verdict);            // человеческий вывод одной строкой

/// <summary>Подборка колод под игрока + честная подпись, откуда взята база.</summary>
public record DeckSuggestionsDto(
    string PlayerTag,
    int MaxCardLevel,
    string BaseUpdated,         // «по состоянию на» — база правится вручную
    string BaseSource,
    int BaseSize,               // сколько колод базы прошло сверку со справочником карт
    int BaseSkipped,            // отброшено из-за незнакомого имени карты (сезонное переименование)
    List<DeckSuggestionDto> Ready,      // можно играть уже сейчас
    List<DeckSuggestionDto> Almost);    // не хватает 1–2 карт
