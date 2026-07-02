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

    /// <summary>Живые данные игрового турнира по тегу (/tournaments/{tag}). null — не найден.</summary>
    Task<CrTournament?> GetTournamentAsync(string tournamentTag, CancellationToken ct = default);

    /// <summary>
    /// Место клана в официальных рейтингах по КВ-трофеям: страна (из профиля клана) и мир.
    /// Ранги только у топ-1000. null — клан не найден или API недоступен.
    /// </summary>
    Task<ClanWarRanking?> GetClanWarRankingAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Военные бои из боевого лога игрока (только КВ-типы). Пустой список — нет/ошибка.</summary>
    Task<List<CrBattle>> GetPlayerBattlelogAsync(string playerTag, CancellationToken ct = default);
}
