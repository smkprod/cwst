using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClanWarTracker.Infrastructure.Persistence.Repositories;

public class WarSnapshotRepository(AppDbContext db) : IWarSnapshotRepository
{
    public async Task UpsertAsync(WarSnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await db.WarSnapshots
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s =>
                s.ClanId == snapshot.ClanId &&
                s.SeasonId == snapshot.SeasonId &&
                s.SectionIndex == snapshot.SectionIndex &&
                s.PeriodIndex == snapshot.PeriodIndex, ct);

        if (existing is null)
        {
            await db.WarSnapshots.AddAsync(snapshot, ct);
        }
        else
        {
            existing.CapturedAtUtc = snapshot.CapturedAtUtc;
            existing.TotalFame = snapshot.TotalFame;
            existing.TotalDecksUsed = snapshot.TotalDecksUsed;
            existing.ParticipantCount = snapshot.ParticipantCount;
            existing.PeriodType = snapshot.PeriodType;
            // "log" (подтверждённый финал) никогда не понижаем обратно до "live":
            // живой тик текущей недели не должен затирать метку качества.
            if (snapshot.Source == "log" || existing.Source != "log") existing.Source = snapshot.Source;

            // Заменяем игроков: состав мог измениться (кик/вступление) — проще пересоздать
            db.PlayerWarSnapshots.RemoveRange(existing.Players);
            existing.Players = snapshot.Players;
        }

        await db.SaveChangesAsync(ct);
    }

    // Чтения ниже — AsNoTracking: эти снимки никогда не изменяются после выборки,
    // а трекинг сотен PlayerWarSnapshot заметно нагружает EF на каждый запрос.
    public Task<List<WarSnapshot>> GetBySeasonAsync(int clanId, int seasonId, CancellationToken ct = default) =>
        db.WarSnapshots.AsNoTracking()
            .Include(s => s.Players)
            .Where(s => s.ClanId == clanId && s.SeasonId == seasonId)
            .OrderBy(s => s.SectionIndex).ThenBy(s => s.PeriodIndex)
            .ToListAsync(ct);

    public Task<WarSnapshot?> GetSnapshotAsync(int clanId, int seasonId, int sectionIndex, int periodIndex,
        CancellationToken ct = default) =>
        db.WarSnapshots.AsNoTracking()
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s =>
                s.ClanId == clanId &&
                s.SeasonId == seasonId &&
                s.SectionIndex == sectionIndex &&
                s.PeriodIndex == periodIndex, ct);

    public async Task<List<PlayerWarSnapshot>> GetPlayerHistoryAsync(string playerTag, int weeks,
        CancellationToken ct = default)
    {
        // Все записи игрока (масштабы MVP позволяют), финал недели выбираем в памяти
        var rows = await db.PlayerWarSnapshots.AsNoTracking()
            .Include(p => p.Snapshot)!.ThenInclude(s => s!.Clan)
            .Where(p => p.PlayerTag == playerTag)
            .ToListAsync(ct);

        return rows
            .Where(r => r.Snapshot is not null)
            .GroupBy(r => (r.Snapshot!.SeasonId, r.Snapshot.SectionIndex, r.Snapshot.ClanId))
            .Select(g => g.OrderByDescending(r => r.Snapshot!.PeriodIndex)
                          .ThenByDescending(r => r.Snapshot!.CapturedAtUtc).First())
            .OrderByDescending(r => r.Snapshot!.SeasonId)
            .ThenByDescending(r => r.Snapshot!.SectionIndex)
            .Take(weeks)
            .ToList();
    }

    public async Task<List<PlayerWarSnapshot>> GetPlayersHistoryAsync(IReadOnlyCollection<string> playerTags,
        int weeks, CancellationToken ct = default)
    {
        // Аналог GetPlayerHistoryAsync, но одним запросом на всех привязанных игроков
        var rows = await db.PlayerWarSnapshots.AsNoTracking()
            .Include(p => p.Snapshot)!.ThenInclude(s => s!.Clan)
            .Where(p => playerTags.Contains(p.PlayerTag))
            .ToListAsync(ct);

        return rows
            .Where(r => r.Snapshot is not null)
            .GroupBy(r => (r.PlayerTag, r.Snapshot!.SeasonId, r.Snapshot.SectionIndex, r.Snapshot.ClanId))
            .Select(g => g.OrderByDescending(r => r.Snapshot!.PeriodIndex)
                          .ThenByDescending(r => r.Snapshot!.CapturedAtUtc).First())
            .GroupBy(r => r.PlayerTag)
            .SelectMany(g => g
                .OrderByDescending(r => r.Snapshot!.SeasonId)
                .ThenByDescending(r => r.Snapshot!.SectionIndex)
                .Take(weeks))
            .ToList();
    }

    public async Task<int?> GetLatestSeasonIdAnyClanAsync(CancellationToken ct = default) =>
        await db.WarSnapshots.Select(s => (int?)s.SeasonId).MaxAsync(ct);

    /// <summary>
    /// Весь сезон по всем кланам. Запрос тяжёлый — шестнадцать кланов по десятку
    /// недель дают десятки тысяч строк игроков, — поэтому вызывающий обязан
    /// кэшировать результат, а не звать это на каждое открытие вкладки.
    /// </summary>
    public Task<List<WarSnapshot>> GetSeasonAcrossClansAsync(int seasonId, CancellationToken ct = default) =>
        db.WarSnapshots.AsNoTracking()
            .Include(s => s.Players)
            .Include(s => s.Clan)
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.ClanId).ThenBy(s => s.SectionIndex).ThenBy(s => s.PeriodIndex)
            .ToListAsync(ct);

    public async Task<int?> GetLatestSeasonIdAsync(int clanId, CancellationToken ct = default) =>
        await db.WarSnapshots
            .Where(s => s.ClanId == clanId)
            .Select(s => (int?)s.SeasonId)
            .MaxAsync(ct);

    public async Task<Dictionary<int, DateTime>> GetLastCapturedByClanAsync(CancellationToken ct = default) =>
        await db.WarSnapshots
            .GroupBy(s => s.ClanId)
            .Select(g => new { ClanId = g.Key, Last = g.Max(s => s.CapturedAtUtc) })
            .ToDictionaryAsync(x => x.ClanId, x => x.Last, ct);

    public async Task<List<int>> GetSeasonIdsAsync(int clanId, CancellationToken ct = default) =>
        await db.WarSnapshots
            .Where(s => s.ClanId == clanId)
            .Select(s => s.SeasonId)
            .Distinct()
            .OrderByDescending(id => id)
            .ToListAsync(ct);

    public async Task<List<WarSnapshot>> GetByClanAsync(int clanId, int weeks, CancellationToken ct = default)
    {
        // Берём последние N недель: ключ недели = (SeasonId, SectionIndex)
        var weekKeys = await db.WarSnapshots
            .Where(s => s.ClanId == clanId)
            .Select(s => new { s.SeasonId, s.SectionIndex })
            .Distinct()
            .OrderByDescending(k => k.SeasonId).ThenByDescending(k => k.SectionIndex)
            .Take(weeks)
            .ToListAsync(ct);

        if (weekKeys.Count == 0) return [];

        // N самых свежих недель - это ровно недели не старше N-й по порядку (сезон, секция).
        // Раньше фильтр был только по сезону, и weeks: 1 тянул весь текущий сезон с игроками.
        var oldest = weekKeys[^1];
        var snapshots = await db.WarSnapshots.AsNoTracking()
            .Include(s => s.Players)
            .Where(s => s.ClanId == clanId
                        && (s.SeasonId > oldest.SeasonId
                            || (s.SeasonId == oldest.SeasonId && s.SectionIndex >= oldest.SectionIndex)))
            .OrderByDescending(s => s.SeasonId)
            .ThenByDescending(s => s.SectionIndex)
            .ThenBy(s => s.PeriodIndex)
            .ToListAsync(ct);

        // Фильтруем по точному набору недель (страховка: условие выше уже точное)
        var keySet = weekKeys.Select(k => (k.SeasonId, k.SectionIndex)).ToHashSet();
        return snapshots.Where(s => keySet.Contains((s.SeasonId, s.SectionIndex))).ToList();
    }

    public async Task<List<(int SeasonId, int SectionIndex)>> GetVerifiedWeeksAsync(int clanId, CancellationToken ct = default)
    {
        var rows = await db.WarSnapshots.AsNoTracking()
            .Where(s => s.ClanId == clanId && s.Source == "log" && s.TotalFame > 0)
            .Select(s => new { s.SeasonId, s.SectionIndex })
            .Distinct()
            .ToListAsync(ct);
        return rows.Select(r => (r.SeasonId, r.SectionIndex)).ToList();
    }
}
