using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Сбор журнала военных боёв: во время военного дня опрашивает боевой лог участников,
/// которые сегодня играли (decksUsedToday &gt; 0 — чтобы не дёргать API впустую), и сохраняет
/// новые бои КВ (кто/когда отыграл и исход). Дедуп по времени последнего сохранённого боя.
/// </summary>
public class CaptureWarBattlesUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IWarBattleRepository warBattles)
{
    /// <returns>Сколько новых боёв сохранено.</returns>
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var batch = new List<WarBattle>();

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            WarStatus? war;
            try { war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct); }
            catch { continue; }
            if (war is null || !war.IsWarDay) continue;

            var active = war.Participants.Where(p => p.DecksUsedToday > 0).ToList();
            if (active.Count == 0) continue;

            foreach (var p in active)
            {
                var last = await warBattles.GetLastBattleTimeAsync(clan.Id, p.PlayerTag, ct);

                List<CrBattle> battles;
                try { battles = await crApi.GetPlayerBattlelogAsync(p.PlayerTag, ct); }
                catch { continue; }

                foreach (var b in battles)
                {
                    if (last is { } l && b.BattleTimeUtc <= l) continue; // уже сохранено
                    batch.Add(new WarBattle
                    {
                        ClanId = clan.Id,
                        PlayerTag = b.PlayerTag,
                        PlayerName = b.PlayerName,
                        BattleTimeUtc = b.BattleTimeUtc,
                        Won = b.Won,
                        CrownsFor = b.CrownsFor,
                        CrownsAgainst = b.CrownsAgainst,
                        SeasonId = war.SeasonId,
                        SectionIndex = war.SectionIndex,
                    });
                }
            }
        }

        if (batch.Count == 0) return 0;

        // Дедуп по (клан, тег, время) — защита от дублей в логе. Единый save за тик:
        // при сбое ничего не сохраняем и пробуем в следующем тике (лог боёв не исчезает).
        var deduped = batch
            .GroupBy(x => (x.ClanId, x.PlayerTag, x.BattleTimeUtc))
            .Select(g => g.First())
            .ToList();

        try
        {
            await warBattles.AddRangeAsync(deduped, ct);
            await warBattles.SaveChangesAsync(ct);
            return deduped.Count;
        }
        catch
        {
            return 0;
        }
    }
}
