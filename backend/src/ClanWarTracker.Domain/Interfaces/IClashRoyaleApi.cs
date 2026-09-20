using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Domain.Interfaces;

public interface IClashRoyaleApi
{
    Task<WarStatus?> GetCurrentWarAsync(string clanTag, CancellationToken ct = default);
    Task<string?> GetPlayerNameAsync(string playerTag, CancellationToken ct = default);
    Task<string?> GetClanNameAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Тег клана, в котором игрок состоит прямо сейчас. null — игрок не найден или без клана.</summary>
    Task<string?> GetPlayerClanTagAsync(string playerTag, CancellationToken ct = default);

    /// <summary>Роли всех текущих участников клана: tag → "leader"/"coLeader"/"elder"/"member". Пустой словарь — ошибка API.</summary>
    Task<Dictionary<string, string>> GetClanMemberRolesAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Состав клана с ролями и кубками (тот же кэшированный ответ, что и роли).</summary>
    Task<Dictionary<string, ClanMemberInfo>> GetClanMembersAsync(string clanTag, CancellationToken ct = default);

    /// <summary>КВ-трофеи клана (clanWarTrophies). null — клан не найден или API недоступен.</summary>
    Task<int?> GetClanWarTrophiesAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Роль игрока в клане: "leader", "coLeader", "elder", "member". null — не найден.</summary>
    Task<string?> GetPlayerClanRoleAsync(string clanTag, string playerTag, CancellationToken ct = default);

    /// <summary>
    /// Журнал завершённых войн клана (официальный /riverracelog, до 10 недель):
    /// места кланов, изменение КВ-трофеев и медали каждого игрока.
    /// Пустой список — клан не найден или API недоступен.
    /// </summary>
    Task<List<RiverRaceLogWeek>> GetRiverRaceLogAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Полный профиль игрока: уровень, трофеи, клан, карты. null — не найден.</summary>
    Task<CrPlayerInfo?> GetPlayerInfoAsync(string playerTag, CancellationToken ct = default);

    /// <summary>
    /// Колоды игроков из мирового топа — чем играют лучшие прямо сейчас.
    /// Тяжёлый запрос (рейтинг + профиль на каждого), поэтому кэшируется надолго.
    /// Пустой список — рейтинг недоступен, это не ошибка.
    /// </summary>
    Task<List<CrTopDeck>> GetTopPlayerDecksAsync(int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Мировой рейтинг по кубкам, до 1000 мест — потолок самого API.
    /// Только строки рейтинга, без профилей: колоды собираются отдельно и параллельно.
    /// </summary>
    Task<List<CrRankedPlayer>> GetGlobalRankingAsync(int limit = 1000, CancellationToken ct = default);

    /// <summary>Живые данные игрового турнира по тегу (/tournaments/{tag}). null — не найден.</summary>
    Task<CrTournament?> GetTournamentAsync(string tournamentTag, CancellationToken ct = default);

    /// <summary>
    /// Место клана в официальных рейтингах по КВ-трофеям: страна (из профиля клана) и мир.
    /// Ранги только у топ-1000. null — клан не найден или API недоступен.
    /// </summary>
    Task<ClanWarRanking?> GetClanWarRankingAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Военные бои из боевого лога игрока (только КВ-типы). Пустой список — нет/ошибка.</summary>
    Task<List<CrBattle>> GetPlayerBattlelogAsync(string playerTag, CancellationToken ct = default);

    /// <summary>
    /// Последние бои игрока любого режима, с колодами обеих сторон.
    /// В отличие от GetPlayerBattlelogAsync ничего не отфильтровывает: там нужны
    /// только военные бои, здесь — то, чем человек играет на самом деле.
    /// </summary>
    Task<List<CrRecentBattle>> GetRecentBattlesAsync(string playerTag, CancellationToken ct = default);

    /// <summary>
    /// Тот же журнал боёв, но с коротким кэшем — для автозачёта результатов турнира.
    ///
    /// Отдельный метод, а не параметр: обычный журнал показывается в карточке игрока,
    /// и держать его пять минут правильно. Автозачёт же опрашивает раз в полминуты,
    /// и пятиминутный кэш означал бы, что результат матча появляется через пять минут
    /// после боя — ровно то, ради чего частый опрос и затевался.
    /// </summary>
    Task<List<CrRecentBattle>> GetBattlesForAutoResultAsync(string playerTag, CancellationToken ct = default);

    /// <summary>
    /// Справочник всех карт игры: имя → карта. Нужен, чтобы показывать карты, которых
    /// у игрока ещё нет (в его профиле API отдаёт только открытые). Пустой словарь — ошибка API.
    /// </summary>
    Task<IReadOnlyDictionary<string, CrCatalogCard>> GetAllCardsAsync(CancellationToken ct = default);

    /// <summary>Численность клана прямо сейчас. null — клан не найден или API недоступен.</summary>
    Task<int?> GetClanMemberCountAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Профиль клана: название, описание, трофеи, требования. null — не найден.</summary>
    Task<CrClanInfo?> GetClanInfoAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Состав клана с именами, ролями, кубками и донатом. Пустой список — ошибка API.</summary>
    Task<List<CrClanMember>> GetClanRosterAsync(string clanTag, CancellationToken ct = default);
}
