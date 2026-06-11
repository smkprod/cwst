using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Сохраняет снимки текущей войны всех кланов (вызывается воркером каждые 30 минут).
/// Upsert по (ClanId, SeasonId, SectionIndex, PeriodIndex) — последний снимок дня = финал.
/// Снимаем только военные дни: тренировка не несёт данных для истории.
/// </summary>
public class CaptureWarSnapshotsUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IWarSnapshotRepository snapshots)
{
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var captured = 0;
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
            if (war is null || !war.IsWarDay) continue;

            await snapshots.UpsertAsync(new WarSnapshot
            {
                ClanId = clan.Id,
                SeasonId = war.SeasonId,
                SectionIndex = war.SectionIndex,
                PeriodIndex = war.PeriodIndex,
                PeriodType = war.PeriodType,
                CapturedAtUtc = DateTime.UtcNow,
                TotalFame = war.Participants.Sum(p => p.Fame),
                TotalDecksUsed = war.Participants.Sum(p => p.DecksUsed),
                ParticipantCount = war.Participants.Count,
                Players = war.Participants.Select(p => new PlayerWarSnapshot
                {
                    PlayerTag = p.PlayerTag,
                    Name = p.Name,
                    Fame = p.Fame,
                    DecksUsed = p.DecksUsed,
                    DecksUsedToday = p.DecksUsedToday,
                    BoatAttacks = p.BoatAttacks,
                    RepairPoints = p.RepairPoints,
                }).ToList(),
            }, ct);
            captured++;
        }
        return captured;
    }
}
