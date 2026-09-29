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
    /// <summary>
    /// Все игроки, привязавшие Telegram (/link), с загруженным кланом.
    ///
    /// Читается БЕЗ отслеживания: список используют витрины, и держать полсотни
    /// сущностей в контексте ради чтения незачем. Менять полученные отсюда записи
    /// нельзя — SaveChanges их не увидит; для изменения бери GetByTagAsync.
    /// </summary>
    Task<List<Player>> GetAllLinkedAsync(CancellationToken ct = default);

    /// <summary>
    /// Игрок по тегу — отслеживаемый, то есть пригодный к изменению.
    ///
    /// Нужен отдельно от GetAllLinkedAsync именно поэтому: тот отдаёт отсоединённые
    /// записи, и правка в них тихо теряется при сохранении.
    /// </summary>
    Task<Player?> GetByTagAsync(string playerTag, CancellationToken ct = default);

    /// <summary>
    /// Игрок по номеру строки — отслеживаемый.
    ///
    /// Точнее тега: тег не уникален, у игрока по строке на каждый клан, где он
    /// бывал. Где известна конкретная строка (счёт на оплату), искать надо по ней.
    /// </summary>
    Task<Player?> GetByIdAsync(int id, CancellationToken ct = default);
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

    /// <summary>Самый свежий сезон с данными по всему сервису. null — снимков нет совсем.</summary>
    Task<int?> GetLatestSeasonIdAnyClanAsync(CancellationToken ct = default);

    /// <summary>
    /// Снимки сезона по всем кланам сразу — для Аллеи славы, которая сравнивает
    /// игроков и кланы между собой, а не внутри одного клана.
    /// </summary>
    Task<List<WarSnapshot>> GetSeasonAcrossClansAsync(int seasonId, CancellationToken ct = default);
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

/// <summary>Ежедневные снимки мирового топа по кубкам.</summary>
public interface ITopPlayerRepository
{
    /// <summary>Есть ли уже снимок за этот день — чтобы не собирать тысячу профилей дважды.</summary>
    Task<bool> HasDayAsync(string dayUtc, CancellationToken ct = default);

    /// <summary>Записывает снимок дня целиком, заменяя прежний за ту же дату.</summary>
    Task ReplaceDayAsync(string dayUtc, IReadOnlyList<TopPlayer> rows, CancellationToken ct = default);

    /// <summary>Снимок за конкретный день, по возрастанию места. Пусто — снимка нет.</summary>
    Task<List<TopPlayer>> GetDayAsync(string dayUtc, CancellationToken ct = default);

    /// <summary>Самая свежая дата, за которую есть снимок. null — снимков нет вовсе.</summary>
    Task<string?> LatestDayAsync(CancellationToken ct = default);

    /// <summary>Даты снимков, новые первыми — для выбора «неделю назад».</summary>
    Task<List<string>> DaysAsync(int limit, CancellationToken ct = default);

    /// <summary>Строка игрока в снимке за день. null — в тот день его в топе не было.</summary>
    Task<TopPlayer?> FindAsync(string dayUtc, string playerTag, CancellationToken ct = default);
}

public interface IPlayerBattleRepository
{
    /// <summary>
    /// Дописывает бои, которых ещё нет (ключ - игрок и время боя), и возвращает,
    /// сколько добавлено. Журнал API перекрывается с прошлым чтением почти целиком.
    /// </summary>
    Task<int> AddNewAsync(string playerTag, IReadOnlyList<PlayerBattle> battles, CancellationToken ct = default);

    /// <summary>Бои игрока начиная с момента, по времени.</summary>
    Task<List<PlayerBattle>> GetSinceAsync(string playerTag, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Бои нескольких игроков за период - для таблицы челленджа, одним запросом.</summary>
    Task<List<PlayerBattle>> GetForTagsBetweenAsync(
        IReadOnlyCollection<string> playerTags, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Бой по номеру. null - такого нет (или стёрт за давностью).</summary>
    Task<PlayerBattle?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Стирает бои старше момента. Возвращает, сколько стёрто.</summary>
    Task<int> PurgeOlderThanAsync(DateTime utc, CancellationToken ct = default);
}

public interface ITiltAlertRepository
{
    Task AddAsync(TiltAlert alert, CancellationToken ct = default);

    /// <summary>Сигнал по номеру — отслеживаемый. null — такого нет.</summary>
    Task<TiltAlert?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Сигналы человека без итога захода — отслеживаемые.</summary>
    Task<List<TiltAlert>> GetOpenAsync(long telegramUserId, CancellationToken ct = default);

    /// <summary>Сигналы человека с момента — для «пауза работает» и счётчиков за день.</summary>
    Task<List<TiltAlert>> GetSinceAsync(long telegramUserId, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Все сигналы с момента — для панели владельца.</summary>
    Task<List<TiltAlert>> GetAllSinceAsync(DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>У кого есть сигналы без итога захода — их надо дописать, даже если Плюс уже кончился.</summary>
    Task<List<long>> UsersWithOpenAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IMetaRepository
{
    /// <summary>
    /// Записывает мету дня целиком, заменяя прежнюю за ту же дату, и стирает дни
    /// старше <paramref name="keepFromDayUtc"/>.
    /// </summary>
    Task ReplaceDayAsync(
        string dayUtc,
        IReadOnlyList<MetaDeckDay> decks,
        IReadOnlyList<MetaMatchupDay> matchups,
        string keepFromDayUtc,
        CancellationToken ct = default,
        IReadOnlyList<MetaBattle>? battles = null);

    /// <summary>Бои топа за дни начиная с <paramref name="fromDayUtc"/> включительно.</summary>
    Task<List<MetaBattle>> GetBattlesSinceAsync(string fromDayUtc, CancellationToken ct = default);

    /// <summary>Колоды за дни начиная с <paramref name="fromDayUtc"/> включительно.</summary>
    Task<List<MetaDeckDay>> GetDecksSinceAsync(string fromDayUtc, CancellationToken ct = default);

    /// <summary>Пары «карта против карты» за дни начиная с <paramref name="fromDayUtc"/>.</summary>
    Task<List<MetaMatchupDay>> GetMatchupsSinceAsync(string fromDayUtc, CancellationToken ct = default);

    /// <summary>Самый свежий день с метой. null — меты ещё нет.</summary>
    Task<string?> LatestDayAsync(CancellationToken ct = default);

    /// <summary>Колоды за окно, сложенные по дням в базе (DayUtc в ответе - начало окна).</summary>
    Task<List<MetaDeckDay>> GetDeckTotalsSinceAsync(string fromDayUtc, CancellationToken ct = default);

    /// <summary>Пары «карта против карты» за окно, сложенные по дням в базе.</summary>
    Task<List<MetaMatchupDay>> GetMatchupTotalsSinceAsync(string fromDayUtc, CancellationToken ct = default);
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

    /// <summary>
    /// Завершённые турниры, свежие первыми: история. Отменённые не показываем —
    /// это не событие, а его отсутствие.
    /// </summary>
    Task<List<Tournament>> GetFinishedAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// Турниры, где есть что закрывать автоматически: сетка собрана или идёт игра,
    /// автозачёт не выключен. С матчами и участниками — они нужны сразу.
    /// </summary>
    Task<List<Tournament>> GetForAutoResultsAsync(CancellationToken ct = default);

    /// <summary>История участия игрока: его записи участника с загруженным турниром, новые — первыми.</summary>
    Task<List<TournamentParticipant>> GetPlayerHistoryAsync(string playerTag, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IServiceModeratorRepository
{
    /// <summary>Все модераторы, новые — первыми. Список короткий, страниц не нужно.</summary>
    Task<List<ServiceModerator>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Найти модератора по тому, что известно о вошедшем: сперва по числовому id,
    /// и только если он ещё ни за кем не закреплён — по юзернейму.
    /// </summary>
    Task<ServiceModerator?> FindAsync(long telegramUserId, string? username, CancellationToken ct = default);

    /// <summary>Запись по id — отслеживаемая, то есть пригодная к удалению.</summary>
    Task<ServiceModerator?> GetByIdAsync(int id, CancellationToken ct = default);

    Task AddAsync(ServiceModerator moderator, CancellationToken ct = default);
    Task RemoveAsync(ServiceModerator moderator, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IServiceSettingRepository
{
    /// <summary>Значение по ключу. null — настройку не трогали, действует умолчание.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Записать значение, создав запись при первом сохранении.</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);
}

public interface IAcquisitionRepository
{
    /// <summary>Откуда пришёл этот человек. null — пришёл без метки.</summary>
    Task<Acquisition?> GetAsync(long telegramUserId, CancellationToken ct = default);

    /// <summary>
    /// Записать источник, если у человека его ещё нет. Первый источник выигрывает.
    /// Сохраняет сам.
    /// </summary>
    /// <returns>true — записано, false — источник уже был.</returns>
    Task<bool> AddIfNewAsync(Acquisition acquisition, CancellationToken ct = default);

    /// <summary>Отметить шаг «привязал тег». Нет записи или уже отмечено — ничего не делает.</summary>
    Task MarkLinkedAsync(long telegramUserId, CancellationToken ct = default);

    /// <summary>Отметить шаг «подключил клан». Нет записи или уже отмечено — ничего не делает.</summary>
    Task MarkClanConnectedAsync(long telegramUserId, CancellationToken ct = default);

    Task<List<Acquisition>> GetAllAsync(CancellationToken ct = default);
}

public interface ICampaignRepository
{
    Task<List<Campaign>> GetAllAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task AddAsync(Campaign campaign, CancellationToken ct = default);
}

public interface ISponsorPaymentRepository
{
    /// <summary>Этот платёж уже учтён — повторная доставка от Telegram.</summary>
    Task<bool> ExistsAsync(string telegramChargeId, CancellationToken ct = default);

    Task AddAsync(SponsorPayment payment, CancellationToken ct = default);

    /// <summary>Последние оплаты, свежие первыми — для панели владельца.</summary>
    Task<List<SponsorPayment>> GetRecentAsync(int limit, CancellationToken ct = default);

    /// <summary>Сколько звёзд пришло за всё время.</summary>
    Task<long> TotalStarsAsync(CancellationToken ct = default);

    /// <summary>Все оплаты — для воронки кампаний: кто из пришедших по ним заплатил.</summary>
    Task<List<SponsorPayment>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Платёж по номеру — отслеживаемый, для отметки о возврате. null — такого нет.</summary>
    Task<SponsorPayment?> GetByChargeAsync(string telegramChargeId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IEntitlementRepository
{
    /// <summary>
    /// До какого момента у человека есть доступ к товару: самый поздний срок среди
    /// не отозванных выдач. null — выдач не было вовсе.
    /// </summary>
    Task<DateTime?> UntilAsync(long telegramUserId, string sku, CancellationToken ct = default);

    /// <summary>Последняя не отозванная выдача — чтобы сказать, откуда доступ (триал, покупка).</summary>
    Task<Entitlement?> LatestAsync(long telegramUserId, string sku, CancellationToken ct = default);

    /// <summary>Был ли уже триал у этого аккаунта или у этого тега.</summary>
    Task<bool> HadTrialAsync(long telegramUserId, string? playerTag, string sku, CancellationToken ct = default);

    Task AddAsync(Entitlement entitlement, CancellationToken ct = default);

    /// <summary>Выдачи по номеру платежа — отслеживаемые, для отзыва при возврате.</summary>
    Task<List<Entitlement>> GetByChargeAsync(string chargeId, CancellationToken ct = default);

    /// <summary>У кого доступ действует прямо сейчас.</summary>
    Task<List<long>> ActiveUsersAsync(string sku, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Последние выдачи, свежие первыми — для панели владельца.</summary>
    Task<List<Entitlement>> GetRecentAsync(int limit, CancellationToken ct = default);

    /// <summary>Все выдачи товара — для статистики продаж и конверсии триала.</summary>
    Task<List<Entitlement>> GetAllAsync(string sku, CancellationToken ct = default);

    /// <summary>Сколько бесплатных подарочных недель человек раздал с указанного момента.</summary>
    Task<int> CountFreeGiftsSinceAsync(long giverTelegramUserId, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>
    /// Выдачи, срок которых кончается в окне, без напоминания - отслеживаемые.
    /// Вызывающий сам проверяет, что это последний срок человека.
    /// </summary>
    Task<List<Entitlement>> GetEndingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IPlayerAlertPrefsRepository
{
    /// <summary>Настройки человека — отслеживаемые. null — он их ещё не трогал.</summary>
    Task<PlayerAlertPrefs?> GetAsync(long telegramUserId, CancellationToken ct = default);

    /// <summary>Настройки человека, заведённые при первом обращении. Отслеживаемые.</summary>
    Task<PlayerAlertPrefs> GetOrCreateAsync(long telegramUserId, CancellationToken ct = default);

    /// <summary>Кто явно включил «Стоп-тильт» — нужны, когда платный доступ выключен владельцем.</summary>
    Task<List<long>> OptedInAsync(CancellationToken ct = default);

    /// <summary>Кто включил «Стоп-тильт» без Плюса и ещё не израсходовал бесплатные сигналы.</summary>
    Task<List<long>> FreeSignalUsersAsync(CancellationToken ct = default);

    /// <summary>У кого уже было знакомство с тильт-типом — чтобы не писать второй раз.</summary>
    Task<HashSet<long>> IntroducedAsync(CancellationToken ct = default);

    /// <summary>У кого стоит пауза — после неё надо написать «можно».</summary>
    Task<List<long>> PausedUsersAsync(CancellationToken ct = default);

    /// <summary>Кого ведёт трекер боёв: включён и личка открыта, или ещё висит карточка захода.</summary>
    Task<List<long>> TrackerUsersAsync(CancellationToken ct = default);

    /// <summary>Для панели владельца: у скольких трекер включён сейчас и сколько его вообще включали.</summary>
    Task<(int Enabled, int Total)> TrackerCountsAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IClanMessageRepository
{
    /// <summary>
    /// Когда этот клан последний раз писал тому. null — ещё не писал.
    /// По этому времени и держится ограничение «раз в сутки на пару».
    /// </summary>
    Task<DateTime?> GetLastSentAtAsync(int fromClanId, int toClanId, CancellationToken ct = default);

    /// <summary>Сколько сообщений клан отправил с указанного момента — защита от веера по всем сразу.</summary>
    Task<int> CountSentSinceAsync(int fromClanId, DateTime sinceUtc, CancellationToken ct = default);

    Task AddAsync(ClanMessage message, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IChallengeRepository
{
    Task<List<ChallengeEntry>> GetEntriesAsync(string eventId, CancellationToken ct = default);
    Task<ChallengeEntry?> GetEntryAsync(string eventId, long telegramUserId, CancellationToken ct = default);
    Task AddAsync(ChallengeEntry entry, CancellationToken ct = default);

    /// <summary>Все события, в которых кто-то участвует, и сколько там участников.</summary>
    Task<List<(string EventId, int Count, DateTime LastJoinedUtc)>> GetEventCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Переносит участников одного события в другое. Кто есть в обоих - остаётся одна
    /// запись, с более ранним вступлением. Возвращает, сколько перенесено.
    /// </summary>
    Task<int> MergeAsync(string fromEventId, string toEventId, CancellationToken ct = default);
}
