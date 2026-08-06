using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Domain.Interfaces;

public interface IClanRepository
{
    Task<Clan?> GetByChatIdAsync(long chatId, CancellationToken ct = default);
    Task<Clan?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Clan?> GetByTagAsync(string clanTag, CancellationToken ct = default);
    Task<List<Clan>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Clan clan, CancellationToken ct = default);
    /// <summary>Удаляет клан вместе с игроками и снапшотами (каскад в БД).</summary>
    Task RemoveAsync(Clan clan, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IPlayerRepository
{
    Task<Player?> GetByTelegramIdAsync(long telegramUserId, CancellationToken ct = default);
    Task<List<Player>> GetByClanIdAsync(int clanId, CancellationToken ct = default);
    /// <summary>Все игроки, привязавшие Telegram (/link), с загруженным кланом.</summary>
    Task<List<Player>> GetAllLinkedAsync(CancellationToken ct = default);
    Task AddAsync(Player player, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IWarSnapshotRepository
{
    /// <summary>Вставка или обновление снимка по ключу (ClanId, SeasonId, SectionIndex, PeriodIndex).</summary>
    Task UpsertAsync(WarSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Снимки клана за последние N недель (с игроками), новые — первыми.</summary>
    Task<List<WarSnapshot>> GetByClanAsync(int clanId, int weeks, CancellationToken ct = default);

    /// <summary>Все снимки клана за конкретный сезон (с игроками).</summary>
    Task<List<WarSnapshot>> GetBySeasonAsync(int clanId, int seasonId, CancellationToken ct = default);

    /// <summary>Последний сезон, по которому есть данные. null — снимков ещё нет.</summary>
    Task<int?> GetLatestSeasonIdAsync(int clanId, CancellationToken ct = default);

    /// <summary>Все сезоны клана с данными, новые — первыми (для архива прошлых сезонов).</summary>
    Task<List<int>> GetSeasonIdsAsync(int clanId, CancellationToken ct = default);

    /// <summary>Один снимок по полному ключу (с игроками). null — не снимали.</summary>
    Task<WarSnapshot?> GetSnapshotAsync(int clanId, int seasonId, int sectionIndex, int periodIndex,
        CancellationToken ct = default);

    /// <summary>
    /// История игрока по всем кланам сервиса: финальный снимок каждой недели,
    /// где игрок участвовал (новые недели первыми). Snapshot и Clan загружены.
    /// </summary>
    Task<List<PlayerWarSnapshot>> GetPlayerHistoryAsync(string playerTag, int weeks,
        CancellationToken ct = default);

    /// <summary>
    /// То же, что GetPlayerHistoryAsync, но для набора игроков сразу
    /// (глобальный топ): до N последних недель на каждого игрока.
    /// </summary>
    Task<List<PlayerWarSnapshot>> GetPlayersHistoryAsync(IReadOnlyCollection<string> playerTags, int weeks,
        CancellationToken ct = default);
}

public interface IWarBattleRepository
{
    /// <summary>Время последнего сохранённого боя игрока — чтобы не перезаписывать старое. null — боёв нет.</summary>
    Task<DateTime?> GetLastBattleTimeAsync(int clanId, string playerTag, CancellationToken ct = default);

    /// <summary>Бои клана за конкретную неделю (сезон+секция), новые первыми.</summary>
    Task<List<WarBattle>> GetByWeekAsync(int clanId, int seasonId, int sectionIndex, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<WarBattle> battles, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRespectRepository
{
    /// <summary>Респект игрока за конкретный день (лимит «1 в сутки»). null — ещё не давал.</summary>
    Task<Respect?> GetByGiverAndDayAsync(string fromPlayerTag, string dayUtc, CancellationToken ct = default);

    /// <summary>Респекты клана за день (для «топа респектов дня»).</summary>
    Task<List<Respect>> GetByClanAndDayAsync(int clanId, string dayUtc, CancellationToken ct = default);

    /// <summary>Сколько респектов получил игрок: всего и начиная с указанного момента.</summary>
    Task<(int Total, int Since)> CountForPlayerAsync(string toPlayerTag, DateTime sinceUtc, CancellationToken ct = default);

    Task AddAsync(Respect respect, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
