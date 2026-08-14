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
            // Сбой по одному клану (CR API недоступен/таймаут) не должен оставлять
            // остальные кланы без свежего снимка в этом тике — иначе следующий
            // отчёт о дне войны соберётся по устаревшим данным.
            try
            {
                // Бэкфилл завершённых недель из официального журнала — копим историю
                // для статистики/прогноза, чтобы она пережила 10-недельное окно журнала.
                try { await BackfillFromRaceLogAsync(clan, ct); }
                catch { /* журнал не критичен */ }

                var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
                if (war is null || !war.IsWarDay) continue;

                // Сохраняем только текущих участников клана (из API, кэш 5 мин).
                var memberTags = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct);
                if (memberTags.Count == 0) continue; // состав неизвестен — лучше пропустить тик,
                                                     // чем записать неполную неделю: следующий
                                                     // тик через 10 минут, а финал всё равно
                                                     // подтвердится журналом.
                var roster = war.Participants.Where(p => memberTags.ContainsKey(p.PlayerTag)).ToList();
                if (roster.Count == 0) continue;     // пустой состав = битые данные, не пишем

                await snapshots.UpsertAsync(new WarSnapshot
                {
                    ClanId = clan.Id,
                    SeasonId = war.SeasonId,
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
            catch { /* этот клан попробуем снова в следующем тике */ }
        }
        return captured;
    }

    /// <summary>
    /// Сохраняет в свою БД завершённые недели из официального журнала (/riverracelog),
    /// которых у нас ещё нет. Журнал отдаёт пофамильные медали последних ~10 войн,
    /// поэтому так подтягиваются даже недели до подключения бота. Финал недели пишем
    /// как снимок последнего военного дня (PeriodIndex=6). Уже имеющиеся недели не трогаем.
    /// </summary>
    private async Task BackfillFromRaceLogAsync(Clan clan, CancellationToken ct)
    {
        var log = await crApi.GetRiverRaceLogAsync(clan.ClanTag, ct);
        if (log.Count == 0) return;

        // Журнал — источник истины по завершённым неделям. Живой снимок мог не поймать
        // финал (день закончился между тиками, API моргнул, бот лежал), и такая неделя
        // тихо оставалась недосчитанной — как колизей 133 сезона.
        // Поэтому пропускаем неделю ТОЛЬКО если она уже подтверждена журналом.
        // Всё остальное перезаписываем, пока неделя в 10-недельном окне журнала.
        var verifiedWeeks = (await snapshots.GetByClanAsync(clan.Id, weeks: 16, ct))
            .Where(s => s.Source == "log" && s.TotalFame > 0)
            .Select(s => (s.SeasonId, s.SectionIndex))
            .ToHashSet();

        foreach (var w in log)
        {
            if (verifiedWeeks.Contains((w.SeasonId, w.SectionIndex))) continue;

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
                Source = "log",                        // подтверждённый финал
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
    }
}
