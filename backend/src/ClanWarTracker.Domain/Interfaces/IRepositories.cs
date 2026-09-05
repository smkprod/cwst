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

    /// <summary>
    /// Запись по тегу, за которой ещё не стоит Telegram-аккаунт, — то есть заготовка,
    /// созданная лидером через /bind. Нужна, чтобы игрок, привязавшийся сам, занял её,
    /// а не завёл вторую строку на тот же тег.
    /// </summary>
    Task<Player?> GetUnclaimedByTagAsync(string playerTag, CancellationToken ct = default);
    /// <summary>Все игроки, привязавшие Telegram (/link), с загруженным кланом.</summary>
    Task<List<Player>> GetAllLinkedAsync(CancellationToken ct = default);
    Task AddAsync(Player player, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Журнал отправленных уведомлений — чтобы рестарт воркера не превращался в повторную
/// рассылку. См. SentNotification.
/// </summary>
public interface ISentNotificationRepository
{
    /// <summary>Ключи этого вида, отправленные позже указанного момента.</summary>
    Task<HashSet<string>> GetKeysAsync(string kind, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Записывает отметку. Повторная запись того же ключа игнорируется.</summary>
    Task AddAsync(string kind, string key, CancellationToken ct = default);

    /// <summary>Удаляет отметки старше указанной даты — таблица не должна расти вечно.</summary>
    Task PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);
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

    /// <summary>
    /// Время последнего снимка по каждому клану (панель владельца: живой ли клан).
    /// Одним запросом, чтобы не дёргать БД по клану.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetLastCapturedByClanAsync(CancellationToken ct = default);

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

    /// <summary>
    /// Бои клана начиная с указанного момента, новые первыми. Нужны карточке дисциплины:
    /// она смотрит на привычки за несколько недель, а не за одну.
    /// </summary>
    Task<List<WarBattle>> GetSinceAsync(int clanId, DateTime sinceUtc, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<WarBattle> battles, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Журнал активных дней: кто заходил и что-то делал в конкретную дату.</summary>
public interface IActivityRepository
{
    /// <summary>
    /// Отмечает, что игрок сегодня был. isAction — было ли это изменяющее действие
    /// (пинок, респект, ответ в игре), а не просто открытие приложения.
    /// Идемпотентно: строка на человека в день.
    /// </summary>
    Task TouchAsync(int playerId, string dayUtc, bool isAction, CancellationToken ct = default);

    /// <summary>
    /// Сколько людей было активно в каждый день начиная с указанной даты:
    /// (день → сколько заходило, сколько что-то делало).
    /// </summary>
    Task<Dictionary<string, (int Active, int Acting)>> GetDailyAsync(string sinceDayUtc, CancellationToken ct = default);
}

public interface IPuzzleRepository
{
    /// <summary>Результат игрока за день. null — сегодня ещё не играл.</summary>
    Task<PuzzleResult?> GetAsync(int playerId, int day, CancellationToken ct = default);

    /// <summary>Создаёт или обновляет запись дня и сразу сохраняет.</summary>
    Task SaveAsync(PuzzleResult result, CancellationToken ct = default);

    /// <summary>
    /// Сколько дней подряд игрок угадывал карту, считая назад от указанного дня.
    /// Сегодняшний день учитывается, только если он уже угадан, — иначе серия
    /// обнулялась бы каждое утро до первой игры.
    /// </summary>
    Task<int> GetStreakAsync(int playerId, int day, CancellationToken ct = default);
}

public interface IRespectRepository
{
    /// <summary>Респект игрока за конкретный день (лимит «1 в сутки»). null — ещё не давал.</summary>
    Task<Respect?> GetByGiverAndDayAsync(string fromPlayerTag, string dayUtc, CancellationToken ct = default);

    /// <summary>Респекты клана за день (для «топа респектов дня»).</summary>
    Task<List<Respect>> GetByClanAndDayAsync(int clanId, string dayUtc, CancellationToken ct = default);

    /// <summary>Сколько респектов получил игрок: всего и начиная с указанного момента.</summary>
    Task<(int Total, int Since)> CountForPlayerAsync(string toPlayerTag, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Сколько респектов роздано по всему сервису с указанного момента.</summary>
    Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default);

    Task AddAsync(Respect respect, CancellationToken ct = default);
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
