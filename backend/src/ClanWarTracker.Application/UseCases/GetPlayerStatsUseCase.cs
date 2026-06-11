using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Детальная статистика по конкретному игроку (для экрана "Моя статистика" в Mini App).
/// </summary>
public class GetPlayerStatsUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    WarForecastService forecast,
    GetSeasonStatsUseCase seasonStats)
{
    public async Task<MyStatsDto?> ExecuteAsync(long telegramUserId, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return null;

        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == player.ClanId);
        if (clan is null) return null;

        var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
        if (war is null) return null;

        var me = war.Participants.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, player.PlayerTag, StringComparison.OrdinalIgnoreCase));
        if (me is null) return null;

        var now = DateTime.UtcNow;
        var hoursLeft = Math.Max(0, (int)war.TimeLeft(now).TotalHours);

        var totalDecksUsed = war.Participants.Sum(p => p.DecksUsed);
        var totalFame = war.Participants.Sum(p => p.Fame);
        // Фолбэк 150 = 50% винрейта (победа 200 / поражение 100)
        var clanAvgFamePerAttack = totalDecksUsed > 0 ? (double)totalFame / totalDecksUsed : 150.0;

        var projection = forecast.ProjectPlayer(me, war, hoursLeft, clanAvgFamePerAttack);

        var ranked = war.Participants.OrderByDescending(p => p.Fame).ToList();
        var rank = ranked.FindIndex(p =>
            string.Equals(p.PlayerTag, me.PlayerTag, StringComparison.OrdinalIgnoreCase)) + 1;

        var contributionPct = totalFame > 0 ? (int)Math.Round(me.Fame * 100.0 / totalFame) : 0;

        var label = PerformanceLabel(projection.AvgFamePerAttack, clanAvgFamePerAttack, rank, war.Participants.Count);

        // Сезонная сводка — только на Pro (как и сезонный зачёт клана)
        MySeasonDto? mySeason = null;
        if (clan.EffectivePlan(now) == Domain.Enums.PlanTier.Pro)
        {
            var season = await seasonStats.ExecuteAsync(clan.Id, war.SeasonId, ct);
            var meRow = season?.Players.FirstOrDefault(p =>
                string.Equals(p.PlayerTag, me.PlayerTag, StringComparison.OrdinalIgnoreCase));
            if (season is not null && meRow is not null)
            {
                mySeason = new MySeasonDto(
                    SeasonId: season.SeasonId,
                    TotalFame: meRow.TotalFame,
                    Rank: meRow.Rank,
                    ClanSize: season.Players.Count,
                    WeeksParticipated: meRow.WeeksParticipated,
                    BestWeekFame: meRow.BestWeekFame,
                    WeeksTracked: season.WeeksTracked);
            }
        }

        return new MyStatsDto(
            PlayerTag: me.PlayerTag,
            Name: me.Name,
            ClanName: clan.Name,
            Fame: me.Fame,
            RepairPoints: me.RepairPoints,
            BoatAttacks: me.BoatAttacks,
            DecksUsedToday: me.DecksUsedToday,
            DecksUsed: me.DecksUsed,
            AvgFamePerAttack: Math.Round(projection.AvgFamePerAttack, 1),
            ProjectedDayFame: projection.ProjectedDayFame,
            ProjectedWeekFame: projection.ProjectedWeekFame,
            Rank: rank,
            ClanSize: war.Participants.Count,
            ContributionPercent: contributionPct,
            PerformanceLabel: label,
            ClanAvgFamePerAttack: Math.Round(clanAvgFamePerAttack, 1),
            Season: mySeason);
    }

    private static string PerformanceLabel(double avgFame, double clanAvg, int rank, int clanSize)
    {
        if (clanSize == 0) return "нет данных";
        var topQuarter = Math.Max(1, clanSize / 4);
        if (rank <= topQuarter && avgFame >= clanAvg * 1.05) return "топ";
        if (avgFame >= clanAvg * 1.05) return "выше среднего";
        if (avgFame >= clanAvg * 0.85) return "средне";
        return "ниже среднего";
    }
}
