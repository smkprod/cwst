namespace ClanWarTracker.Application.DTOs;

/// <summary>Бой в карточке игрока. Result: 1 - победа, 0 - ничья, -1 - поражение.</summary>
public record SheetBattleDto(
    string TimeUtc,
    string Type,
    int Result,
    int CrownsFor,
    int CrownsAgainst,
    string? OpponentName,
    string? OpponentTag,
    int? TrophyChange,
    List<MetaCardDto> MyDeck,
    List<MetaCardDto> OpponentDeck);

/// <summary>
/// Единая карточка игрока - одна на весь бот: поиск, КВ, мировой топ, аллея.
///
/// Здесь только то, что одинаково для любого места открытия: профиль из игры,
/// колода, бои и всё, что знает о человеке бот (спонсорство, место в топе,
/// разбор). КВ-строку клиент берёт из того списка, откуда карточку открыли,
/// или дозапрашивает статус клана - она живая и считается там.
/// </summary>
/// <param name="Rating">Рейтинг Пути легенд в текущем сезоне; null - не играл.</param>
/// <param name="WorldRank">Место в последнем снимке мирового топа; null - не в топе.</param>
/// <param name="InBot">Игрок привязал Telegram к боту.</param>
/// <param name="Games30">Боёв за 30 дней, сохранённых ботом (только у тех, кто в боте).</param>
public record PlayerSheetDto(
    string PlayerTag,
    string Name,
    int ExpLevel,
    string? ClanName,
    string? ClanTag,
    string? Role,
    string? ArenaName,
    int Trophies,
    int BestTrophies,
    int? Rating,
    int? RatingLeague,
    int? RatingRank,
    int? BestRating,
    int Wins,
    int Losses,
    int ThreeCrownWins,
    int BattleCount,
    int WarDayWins,
    int ClanWarTrophies,
    int CurrentStreak,
    MetaCardDto? FavouriteCard,
    int? WorldRank,
    bool InBot,
    bool IsSponsor,
    string? BackgroundKey,
    string? BadgeKey,
    int BadgeLevel,
    List<MetaCardDto> Deck,
    double DeckElixir,
    string? DeckLink,
    List<SheetBattleDto> Battles,
    int Games30,
    double WinPercent30,
    string RoyaleApiUrl,
    DuelSheetDto? Duel = null);

/// <summary>Ранг в лиге дуэлей Clanify. Нет - игрок в лигу не вступал.</summary>
public record DuelSheetDto(int Trophies, int Peak, string League, int Division, int Wins, int Losses, int Place);
