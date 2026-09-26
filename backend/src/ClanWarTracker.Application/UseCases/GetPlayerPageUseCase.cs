using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Страница игрока с Аллеи.
///
/// Показывает только витрину: место, медали, значки, оформление. Ни советов, ни
/// прогнозов, ни слабых мест — всё это есть на собственной вкладке «Я» и там
/// уместно, а на чужой странице превратилось бы в досье.
/// </summary>
public class GetPlayerPageUseCase(
    GetHallOfFameUseCase hall,
    GetAchievementsUseCase achievements,
    IPlayerRepository players,
    IClanRepository clans,
    IWarSnapshotRepository snapshots)
{
    /// <summary>Сколько недель истории берём на график. Сезон короче, запас на стык.</summary>
    private const int WeeksWindow = 12;

    public async Task<PlayerPageDto?> ExecuteAsync(
        string playerTag, long viewerTelegramId, CancellationToken ct = default)
    {
        var data = await hall.LoadAsync(ct);
        if (data is null) return null;

        var row = data.Players.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
        if (row is null) return null;

        var viewer = await players.GetByTelegramIdAsync(viewerTelegramId, ct);

        // Клан ищем по тегу из зачёта, а не по ClanId игрока: в зачёте он привязан
        // к клану, за который набивал, и переход со страницы должен вести туда же.
        var clan = row.ClanTag is null ? null : await clans.GetByTagAsync(row.ClanTag, ct);

        var weeks = await WeeksAsync(row.PlayerTag, data.SeasonId, ct);

        // Значки считаются по истории клана, поэтому без клана их просто нет.
        // Это не ошибка: игрок вне зачёта клана не мог ничего выбить.
        List<PlayerPageBadgeDto> badges = [];
        if (clan is not null)
        {
            var earned = await achievements.ExecuteAsync(clan.Id, row.PlayerTag, ct);
            badges = earned.Badges
                .Where(b => b.Level > 0)
                .OrderByDescending(b => b.Level)
                .Select(b => new PlayerPageBadgeDto(b.Key, b.Level, b.Value))
                .ToList();
        }

        return new PlayerPageDto(
            Rank: row.Rank,
            TotalPlayers: data.Players.Count,
            SeasonId: data.SeasonId,
            PlayerTag: row.PlayerTag,
            Name: row.Name,
            ClanName: row.ClanName,
            ClanTag: row.ClanTag,
            ClanId: clan?.Id,
            SeasonFame: row.SeasonFame,
            WeeksPlayed: row.WeeksPlayed,
            BestWeekFame: weeks.Count == 0 ? 0 : weeks.Max(w => w.Fame),
            IsSponsor: row.IsSponsor,
            BackgroundKey: row.BackgroundKey,
            ShowcaseKey: row.BadgeKey,
            ShowcaseLevel: row.BadgeLevel,
            IsMe: viewer is not null
                  && string.Equals(viewer.PlayerTag, row.PlayerTag, StringComparison.OrdinalIgnoreCase),
            Badges: badges,
            Weeks: weeks);
    }

    /// <summary>
    /// Медали игрока по неделям текущего сезона — тот же максимум-за-неделю, что и везде.
    ///
    /// Сезон фильтруем обязательно: история приходит сквозной, а номер недели
    /// внутри сезона начинается заново. Без фильтра первая неделя нового сезона
    /// легла бы поверх первой недели прошлого и выдала бы на графике максимум из двух.
    /// </summary>
    private async Task<List<SeasonWeekDto>> WeeksAsync(string playerTag, int seasonId, CancellationToken ct)
    {
        var history = await snapshots.GetPlayerHistoryAsync(playerTag, WeeksWindow, ct);
        return history
            .Where(h => h.Snapshot is not null && h.Snapshot.SeasonId == seasonId)
            .GroupBy(h => h.Snapshot!.SectionIndex)
            .Select(g => new SeasonWeekDto(g.Key, g.Max(h => h.Fame)))
            .Where(w => w.Fame > 0)
            .OrderBy(w => w.SectionIndex)
            .ToList();
    }
}
