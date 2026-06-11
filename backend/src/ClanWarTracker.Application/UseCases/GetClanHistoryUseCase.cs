using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>История войн клана по неделям из накопленных снапшотов (Pro-фича).</summary>
public class GetClanHistoryUseCase(IWarSnapshotRepository snapshots)
{
    public async Task<ClanHistoryDto> ExecuteAsync(
        int clanId, int weeks = 8, string? myPlayerTag = null, CancellationToken ct = default)
    {
        var all = await snapshots.GetByClanAsync(clanId, weeks, ct);

        var weekDtos = all
            .GroupBy(s => (s.SeasonId, s.SectionIndex))
            .OrderByDescending(g => g.Key.SeasonId)
            .ThenByDescending(g => g.Key.SectionIndex)
            .Select(g =>
            {
                var ordered = g.OrderBy(s => s.PeriodIndex).ToList();
                var last = ordered[^1];

                // Прирост за день = слава дня минус слава предыдущего дня (она накопительная)
                var days = new List<DayHistoryDto>();
                var prevFame = 0;
                foreach (var s in ordered)
                {
                    days.Add(new DayHistoryDto(
                        PeriodIndex: s.PeriodIndex,
                        DayNumber: Math.Clamp(s.PeriodIndex - 2, 1, 4),
                        CapturedAtUtc: s.CapturedAtUtc,
                        TotalFame: s.TotalFame,
                        DayFame: Math.Max(0, s.TotalFame - prevFame)));
                    prevFame = s.TotalFame;
                }

                var top = last.Players
                    .OrderByDescending(p => p.Fame)
                    .Take(3)
                    .Select(p => new TopPlayerDto(p.PlayerTag, p.Name, p.Fame))
                    .ToList();

                int? myFame = myPlayerTag is null
                    ? null
                    : last.Players.FirstOrDefault(p =>
                        string.Equals(p.PlayerTag, myPlayerTag, StringComparison.OrdinalIgnoreCase))?.Fame ?? 0;

                return new WeekHistoryDto(
                    SeasonId: g.Key.SeasonId,
                    SectionIndex: g.Key.SectionIndex,
                    IsColosseum: ordered.Any(s => s.PeriodType == "colosseum"),
                    FinalFame: last.TotalFame,
                    MyFame: myFame,
                    Days: days,
                    TopPlayers: top);
            })
            .ToList();

        return new ClanHistoryDto(weekDtos);
    }
}
