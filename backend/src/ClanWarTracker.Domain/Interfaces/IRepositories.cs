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

public interface IGameTournamentRepository
{
    /// <summary>Все отслеживаемые игровые турниры, новые — первыми.</summary>
    Task<List<GameTournament>> GetAllAsync(CancellationToken ct = default);
    Task<GameTournament?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<GameTournament?> GetByTagAsync(string tournamentTag, CancellationToken ct = default);
    Task AddAsync(GameTournament tournament, CancellationToken ct = default);
    Task RemoveAsync(GameTournament tournament, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRecruitmentRepository
{
    Task<RecruitmentProfile?> GetByPlayerTagAsync(string playerTag, CancellationToken ct = default);
    Task<List<RecruitmentProfile>> GetActiveAsync(CancellationToken ct = default);
    Task UpsertAsync(RecruitmentProfile profile, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITournamentRepository
{
    /// <summary>Турнир с загруженными участниками и матчами (с навигацией на участников матча).</summary>
    Task<Tournament?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Турниры, ещё не завершённые и не отменённые (для вкладки "Турниры"), новые — первыми.</summary>
    Task<List<Tournament>> GetActiveAsync(CancellationToken ct = default);

    Task AddAsync(Tournament tournament, CancellationToken ct = default);

    /// <summary>
    /// Атомарно проверяет лимит активных турниров создателя и, если он не превышен,
    /// добавляет турнир и сохраняет — в одной сериализуемой транзакции. Возвращает false,
    /// если лимит уже достигнут (в т.ч. из-за гонки одновременных запросов — против спама).
    /// </summary>
    Task<bool> TryAddWithinActiveLimitAsync(Tournament tournament, long creatorTelegramUserId,
        int maxActive, CancellationToken ct = default);

    /// <summary>История участия игрока: его записи участника с загруженным турниром, новые — первыми.</summary>
    Task<List<TournamentParticipant>> GetPlayerHistoryAsync(string playerTag, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
