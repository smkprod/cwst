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
            // Бэкфилл завершённых недель из официального журнала — копим историю
            // для статистики/прогноза, чтобы она пережила 10-недельное окно журнала.
            // Журнал возвращаем, чтобы определить реальный seasonId для текущей недели
            // (/currentriverrace всегда отдаёт seasonId=0 — баг CR API).
            List<Domain.Entities.RiverRaceLogWeek> raceLog = [];
            try { raceLog = await BackfillFromRaceLogAsync(clan, ct); }
            catch { /* журнал не критичен */ }

            var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
            if (war is null || !war.IsWarDay) continue;

            var realSeasonId = raceLog.Count > 0
                ? (raceLog[0].SectionIndex >= 3 ? raceLog[0].SeasonId + 1 : raceLog[0].SeasonId)
                : war.SeasonId;

            // Сохраняем только текущих участников клана (из API, кэш 5 мин).
            // Фолбэк на топ-50 самых активных, если API недоступен.
            var memberTags = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct);
            var roster = memberTags.Count > 0
                ? war.Participants.Where(p => memberTags.ContainsKey(p.PlayerTag)).ToList()
                : war.Participants
                    .OrderByDescending(p => p.DecksUsedToday)
                    .ThenByDescending(p => p.DecksUsed)
                    .ThenByDescending(p => p.Fame)
                    .Take(50)
                    .ToList();

            await snapshots.UpsertAsync(new WarSnapshot
            {
                ClanId = clan.Id,
                SeasonId = realSeasonId,
                SectionIndex = war.SectionIndex,
                PeriodIndex = war.PeriodIndex,
                PeriodType = war.PeriodType,
                CapturedAtUtc = DateTime.UtcNow,
                TotalFame = roster.Sum(p => p.Fame),
                TotalDecksUsed = roster.Sum(p => p.DecksUsed),
                ParticipantCount = roster.Count,
                Players = roster.Select(p => new PlayerWarSnapshot
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

    /// <summary>
    /// Сохраняет в свою БД завершённые недели из официального журнала (/riverracelog),
    /// которых у нас ещё нет. Журнал отдаёт пофамильные медали последних ~10 войн,
    /// поэтому так подтягиваются даже недели до подключения бота. Финал недели пишем
    /// как снимок последнего военного дня (PeriodIndex=6). Уже имеющиеся недели не трогаем.
    /// </summary>
    private async Task<List<Domain.Entities.RiverRaceLogWeek>> BackfillFromRaceLogAsync(Clan clan, CancellationToken ct)
    {
        var log = await crApi.GetRiverRaceLogAsync(clan.ClanTag, ct);
        if (log.Count == 0) return log;

        var existing = (await snapshots.GetByClanAsync(clan.Id, weeks: 16, ct))
            .Select(s => (s.SeasonId, s.SectionIndex))
            .ToHashSet();

        foreach (var w in log)
        {
            if (existing.Contains((w.SeasonId, w.SectionIndex))) continue;

            var ours = w.Standings.FirstOrDefault(s =>
                string.Equals(s.ClanTag, clan.ClanTag, StringComparison.OrdinalIgnoreCase));
            if (ours is null || ours.Participants.Count == 0) continue;

            await snapshots.UpsertAsync(new WarSnapshot
            {
                ClanId = clan.Id,
                SeasonId = w.SeasonId,
                SectionIndex = w.SectionIndex,
                PeriodIndex = 6,                       // финал недели (последний военный день)
                PeriodType = w.IsColosseum ? "colosseum" : "warDay",
                CapturedAtUtc = DateTime.UtcNow,
                TotalFame = ours.Fame,
                TotalDecksUsed = ours.Participants.Sum(p => p.DecksUsed),
                ParticipantCount = ours.Participants.Count,
                Players = ours.Participants.Select(p => new PlayerWarSnapshot
                {
                    PlayerTag = p.PlayerTag,
                    Name = p.Name,
                    Fame = p.Fame,
                    DecksUsed = p.DecksUsed,
                    DecksUsedToday = 0,   // журнал не разбивает по дням
                    BoatAttacks = 0,
                    RepairPoints = 0,
                }).ToList(),
            }, ct);
        }
        return log;
    }
}
