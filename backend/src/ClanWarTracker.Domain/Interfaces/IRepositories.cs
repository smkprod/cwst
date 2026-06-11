using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Domain.Interfaces;

public interface IClanRepository
{
    Task<Clan?> GetByChatIdAsync(long chatId, CancellationToken ct = default);
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
}
