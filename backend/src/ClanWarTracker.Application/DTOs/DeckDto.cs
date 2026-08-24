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
    string Rarity,              // common | rare | epic | legendary | champion
    string IconUrl,
    bool Owned,
    bool EvoUnlocked,           // игрок открыл эволюцию этой карты
    bool HasEvo,                // у карты вообще есть эволюция
    string? EvoIconUrl);

/// <summary>Сколько карт колоды приходится на одну редкость.</summary>
public record DeckRarityDto(string Rarity, int Count);

/// <summary>
/// Колода меты, примеренная на коллекцию конкретного игрока.
/// Никаких оценок вроде «сложности» — только то, что можно посчитать.
/// </summary>
public record DeckSuggestionDto(
    string Id,
    string Name,
    string Archetype,
    string Note,
    List<DeckCardDto> Cards,
    int OwnedCount,             // сколько из 8 карт открыто
    List<string> Missing,       // чего не хватает (имена карт)
    double AvgLevel,            // средний уровень открытых карт колоды
    double AvgElixir,
    int CycleCost,              // 4 самые дешёвые карты — за сколько эликсира колода прокручивается
    int LevelsToMax,            // сколько уровней ещё качать по уже открытым картам
    List<DeckRarityDto> Rarity, // состав по редкостям: во что обойдётся прокачка
    int MaxedCount,             // карт на потолке уровня
    int EvoUnlocked,            // эволюций колоды открыто у игрока
    int EvoAvailable,           // сколько карт колоды вообще имеют эволюцию
    int Readiness,              // 0..100 — насколько колода готова прямо сейчас
    string Verdict,             // человеческий вывод одной строкой
    // Ссылка вида link.clashroyale.com/deck — тап открывает колоду прямо в игре.
    // null, если у какой-то карты нет id в справочнике: половина колоды хуже, чем
    // отсутствие кнопки, — человек тапнет и получит мусор вместо колоды.
    string? CopyLink);

/// <summary>
/// Колода игрока из мирового топа, примеренная на коллекцию того, кто смотрит.
///
/// Ценность в имени: «так играет игрок №1 мира» весомее любой усреднённой меты.
/// И это единственный источник колод, который не надо править руками, — в отличие
/// от MetaDecks, устаревающего каждый сезон.
/// </summary>
public record TopDeckDto(
    string PlayerName,
    int Rank,
    int Trophies,
    string? ClanName,
    DeckSuggestionDto Deck);

/// <summary>Подборка колод под игрока + честная подпись, откуда взята база.</summary>
public record DeckSuggestionsDto(
    string PlayerTag,
    int MaxCardLevel,
    string BaseUpdated,         // «по состоянию на» — база правится вручную
    string BaseSource,
    int BaseSize,               // сколько колод базы прошло сверку со справочником карт
    int BaseSkipped,            // отброшено из-за незнакомого имени карты (сезонное переименование)
    List<DeckSuggestionDto> Ready,      // можно играть уже сейчас
    List<DeckSuggestionDto> Almost,     // не хватает 1–2 карт
    List<TopDeckDto> Top);              // что играют лучшие в мире прямо сейчас
