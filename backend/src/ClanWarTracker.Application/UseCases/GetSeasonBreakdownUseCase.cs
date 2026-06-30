using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Разбивка текущего сезона по неделям. Завершённые недели берём из официального
/// журнала (/riverracelog) — там пофамильные медали даже без наших снимков,
/// поэтому W1 виден, даже если бот тогда ещё не собирал данные.
/// Текущую (идущую) неделю — из live-данных гонки. Плюс общий зачёт за сезон.
/// </summary>
public class GetSeasonBreakdownUseCase(IClashRoyaleApi crApi)
{
    public async Task<SeasonBreakdownDto?> ExecuteAsync(string clanTag, CancellationToken ct = default)
    {
        var war = await crApi.GetCurrentWarAsync(clanTag, ct);
        var log = await crApi.GetRiverRaceLogAsync(clanTag, ct);

        // Текущий сезон уже корректно вычислен в GetCurrentWarAsync (ResolveRealSeasonIdAsync:
        // сезон последней завершённой недели + переход только когда текущая неделя = section 0).
        // Здесь его НЕ пересчитываем по "section >= 3" — это ломалось на 5-недельных сезонах
        // (колизей = section 4), из-за чего бот раньше времени показывал следующий сезон.
        int seasonId;
        if (war is not null)
            seasonId = war.SeasonId;
        else if (log.Count > 0)
            seasonId = log[0].SeasonId; // фолбэк (CR API лежит): сезон последней завершённой недели
        else
            return null;
        if (seasonId == 0 && war is null) return null;

        // sectionIndex -> накопитель недели
        var weeks = new Dictionary<int, WeekAcc>();

        // 1) Завершённые недели ТЕКУЩЕГО сезона из официального журнала (пофамильно)
        foreach (var w in log.Where(w => w.SeasonId == seasonId))
        {
            var ours = w.Standings.FirstOrDefault(s =>
                string.Equals(s.ClanTag, clanTag, StringComparison.OrdinalIgnoreCase));
            if (ours is null) continue;

            var acc = new WeekAcc { ClanFame = ours.Fame };
            foreach (var p in ours.Participants)
                acc.Players[p.PlayerTag] = (p.Name, p.Fame, p.DecksUsed);
            weeks[w.SectionIndex] = acc;
        }

        // 2) Текущая (идущая) неделя из live-гонки — если её секции ещё нет в журнале
        if (war is not null && !weeks.ContainsKey(war.SectionIndex))
        {
            var memberTags = await crApi.GetClanMemberRolesAsync(clanTag, ct);
            var roster = memberTags.Count > 0
                ? war.Participants.Where(p => memberTags.ContainsKey(p.PlayerTag))
                : war.Participants;

            var acc = new WeekAcc { IsCurrent = true };
            foreach (var p in roster)
            {
                acc.Players[p.PlayerTag] = (p.Name, p.Fame, p.DecksUsed);
                acc.ClanFame += p.Fame;
            }
            weeks[war.SectionIndex] = acc;
        }

        if (weeks.Count == 0) return null;

        var currentSection = war?.SectionIndex ?? weeks.Keys.Max();

        // Колизей в разбивке текущего сезона — это ТОЛЬКО идущая сейчас неделя, если CR
        // сообщает periodType == "colosseum". Завершённые недели текущего сезона колизеем
        // быть не могут (колизей всегда последний). Никаких "section == 3".
        var colosseumNow = war?.PeriodType == "colosseum";

        var weekDtos = weeks
            .OrderBy(kv => kv.Key)
            .Select(kv => new SeasonWeekDto(
                SectionIndex: kv.Key,
                Label: kv.Value.IsCurrent && colosseumNow ? "Колизей" : $"Война {kv.Key + 1}",
                IsColosseum: kv.Value.IsCurrent && colosseumNow,
                IsCurrent: kv.Value.IsCurrent,
                ClanFame: kv.Value.ClanFame,
                Players: kv.Value.Players
                    .OrderByDescending(p => p.Value.Fame)
                    .Select((p, i) => new SeasonWeekPlayerDto(
                        PlayerTag: p.Key,
                        Name: p.Value.Name,
                        Fame: p.Value.Fame,
                        DecksUsed: p.Value.Decks,
                        Rank: i + 1))
                    .ToList()))
            .ToList();

        // Общий зачёт за сезон: сумма медалей игрока по всем неделям
        var agg = new Dictionary<string, (string Name, int Total, int Weeks, int Best)>(StringComparer.OrdinalIgnoreCase);
        foreach (var week in weeks.Values)
            foreach (var (tag, data) in week.Players)
            {
                agg.TryGetValue(tag, out var cur);
                agg[tag] = (data.Name,
                    cur.Total + data.Fame,
                    cur.Weeks + (data.Fame > 0 ? 1 : 0),
                    Math.Max(cur.Best, data.Fame));
            }

        var total = agg
            .OrderByDescending(kv => kv.Value.Total)
            .Select((kv, i) => new SeasonPlayerDto(
                PlayerTag: kv.Key,
                Name: kv.Value.Name,
                TotalFame: kv.Value.Total,
                WeeksParticipated: kv.Value.Weeks,
                BestWeekFame: kv.Value.Best,
                Rank: i + 1))
            .ToList();

        return new SeasonBreakdownDto(seasonId, currentSection, weekDtos, total);
    }

    private sealed class WeekAcc
    {
        public Dictionary<string, (string Name, int Fame, int Decks)> Players { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public int ClanFame { get; set; }
        public bool IsCurrent { get; set; }
    }
}
