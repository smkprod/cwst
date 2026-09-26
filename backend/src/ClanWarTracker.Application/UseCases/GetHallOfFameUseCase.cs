using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Аллея славы: лучшие игроки и кланы сервиса за сезон.
///
/// Сезонный зачёт внутри клана уже считался, но он отвечал на «кто лучший у нас».
/// Здесь вопрос другой — «кто лучший вообще», и именно он даёт повод показывать
/// себя: место в своём клане видят полсотни человек, место на Аллее видят все.
/// </summary>
public class GetHallOfFameUseCase(
    IWarSnapshotRepository snapshots,
    IPlayerRepository players,
    IMemoryCache cache)
{
    /// <summary>
    /// Сколько держим посчитанное.
    ///
    /// Расчёт читает сезон по всем кланам разом — десятки тысяч строк. Снимки
    /// обновляются раз в десять минут, поэтому чаще считать нечего, а пять минут
    /// оставляют запас на случай, если вкладку откроют разом всем составом.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Сколько строк отдаём.
    ///
    /// Было сто. Сотня мотивирует того, кто в неё попал, и ровно ничего не говорит
    /// остальным: игроков в зачёте уже вдвое больше, и человеку на сто двадцатом
    /// месте список сообщал только то, что его в нём нет. Две сотни покрывают
    /// почти весь зачёт, а вес выдачи при этом остаётся в пределах сотни килобайт.
    /// </summary>
    private const int Limit = 200;

    /// <param name="viewerTag">
    /// Тег смотрящего — чтобы вернуть его собственную строку. Считается из уже
    /// готового списка, поэтому персональная часть кэш не ломает: тяжёлый расчёт
    /// общий на всех, а своё место вырезается из него на каждом запросе.
    /// </param>
    public async Task<HallOfFameDto?> ExecuteAsync(string? viewerTag = null, CancellationToken ct = default)
    {
        var data = await LoadAsync(ct);
        if (data is null) return null;

        var me = viewerTag is null
            ? null
            : data.Players.FirstOrDefault(p =>
                string.Equals(p.PlayerTag, viewerTag, StringComparison.OrdinalIgnoreCase));

        return new HallOfFameDto(
            data.SeasonId,
            data.ClansCounted,
            data.Players.Count,
            me,
            data.Players.Take(Limit).ToList(),
            data.Clans.Take(Limit).ToList());
    }

    /// <summary>
    /// Весь посчитанный сезон, без обрезки по <see cref="Limit"/>.
    ///
    /// Нужен страницам клана и игрока: у них вопрос не «кто в топе», а «какое место
    /// вот у этого», и ответ обязан находиться и для двухсотпятидесятого. Считать
    /// ради этого второй раз нечего — расчёт один и тот же, и кэш тоже один.
    /// </summary>
    public Task<HallData?> LoadAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync("halloffame", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await BuildAsync(ct);
        });

    /// <summary>Посчитанный сезон целиком — из него уже режутся и топ, и своя строка.</summary>
    public record HallData(int SeasonId, int ClansCounted, List<HallPlayerDto> Players, List<HallClanDto> Clans);

    private async Task<HallData?> BuildAsync(CancellationToken ct)
    {
        var seasonId = await snapshots.GetLatestSeasonIdAnyClanAsync(ct);
        if (seasonId is null) return null;

        var all = await snapshots.GetSeasonAcrossClansAsync(seasonId.Value, ct);
        if (all.Count == 0) return null;

        // Финал недели для каждого клана — та же логика, что в клановом зачёте:
        // слава за неделю только растёт, поэтому максимум и есть итог. Снимок,
        // подтверждённый официальным журналом, предпочтительнее живого.
        var weekFinals = all
            .GroupBy(s => (s.ClanId, s.SectionIndex))
            .Select(g => g
                .OrderByDescending(s => s.Source == "log" && s.TotalFame > 0)
                .ThenByDescending(s => s.TotalFame)
                .ThenByDescending(s => s.PeriodIndex)
                .First())
            .ToList();

        var playerRows = AggregatePlayers(weekFinals);
        var clanRows = AggregateClans(weekFinals);

        // Привязки и спонсорство подтягиваем одним заходом: ходить в базу на
        // каждого из сотни игроков — это сотня запросов ради одной таблицы.
        var linked = await players.GetAllLinkedAsync(ct);
        var byTag = linked
            .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;

        var topPlayers = playerRows
            .OrderByDescending(r => r.Fame)
            .Select((r, i) =>
            {
                byTag.TryGetValue(r.Tag, out var player);
                var sponsor = player?.IsSponsor(now) == true;
                return new HallPlayerDto(
                    Rank: i + 1,
                    PlayerTag: r.Tag,
                    Name: r.Name,
                    ClanName: r.ClanName,
                    ClanTag: r.ClanTag,
                    SeasonFame: r.Fame,
                    WeeksPlayed: r.Weeks,
                    BadgeKey: player?.ShowcaseBadgeKey,
                    BadgeLevel: player?.ShowcaseBadgeLevel ?? 0,
                    IsSponsor: sponsor,
                    // Фон показываем только действующему спонсору: истёкшее
                    // спонсорство не должно продолжать красить строку.
                    BackgroundKey: sponsor ? SponsorBackground.NormalizePlayer(player?.SponsorBackgroundKey) : null);
            })
            .ToList();

        // Спонсоры по кланам — клан с ними получает вызовы и свой фон на Аллее.
        var sponsorsByClan = linked
            .Where(p => p.IsSponsor(now) && p.ClanId.HasValue)
            .GroupBy(p => p.ClanId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var topClans = clanRows
            .OrderByDescending(r => r.Fame)
            .Select((r, i) =>
            {
                sponsorsByClan.TryGetValue(r.ClanId, out var clanSponsors);
                return new HallClanDto(
                    Rank: i + 1,
                    ClanId: r.ClanId,
                    ClanTag: r.ClanTag,
                    ClanName: r.ClanName,
                    SeasonFame: r.Fame,
                    WeeksPlayed: r.Weeks,
                    SponsorCount: clanSponsors?.Count ?? 0,
                    // Фон клана задаёт его спонсор. Если спонсоров несколько и они
                    // выбрали разное — берём первый непустой, а не «последний
                    // победил»: иначе фон клана прыгал бы от перезагрузки к перезагрузке.
                    BackgroundKey: clanSponsors?
                        .OrderBy(p => p.Id)
                        .Select(p => SponsorBackground.NormalizeClan(p.SponsorClanBackgroundKey))
                        .FirstOrDefault(k => k is not null));
            })
            .ToList();

        return new HallData(seasonId.Value, clanRows.Count, topPlayers, topClans);
    }

    private record PlayerRow(string Tag, string Name, string ClanName, string? ClanTag, int Fame, int Weeks);
    private record ClanRow(int ClanId, string ClanTag, string ClanName, int Fame, int Weeks);

    /// <summary>
    /// Игрок суммируется по всем неделям сезона. Если он за сезон сменил клан,
    /// показываем тот, за который он играл последним: его медали заработаны в
    /// обоих, но «сейчас он здесь» — единственное, что читается однозначно.
    /// </summary>
    private static List<PlayerRow> AggregatePlayers(List<WarSnapshot> weekFinals)
    {
        var agg = new Dictionary<string, PlayerRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var week in weekFinals.OrderBy(w => w.SectionIndex))
        {
            var clanName = week.Clan?.Name ?? "—";
            var clanTag = week.Clan?.ClanTag;

            foreach (var p in week.Players)
            {
                agg.TryGetValue(p.PlayerTag, out var cur);
                agg[p.PlayerTag] = new PlayerRow(
                    Tag: p.PlayerTag,
                    Name: p.Name,
                    ClanName: clanName,
                    ClanTag: clanTag,
                    Fame: (cur?.Fame ?? 0) + p.Fame,
                    Weeks: (cur?.Weeks ?? 0) + (p.Fame > 0 ? 1 : 0));
            }
        }

        return agg.Values.Where(r => r.Fame > 0).ToList();
    }

    private static List<ClanRow> AggregateClans(List<WarSnapshot> weekFinals)
    {
        var agg = new Dictionary<int, ClanRow>();

        foreach (var week in weekFinals)
        {
            if (week.Clan is null) continue;
            agg.TryGetValue(week.ClanId, out var cur);
            agg[week.ClanId] = new ClanRow(
                ClanId: week.ClanId,
                ClanTag: week.Clan.ClanTag,
                ClanName: week.Clan.Name,
                Fame: (cur?.Fame ?? 0) + week.TotalFame,
                Weeks: (cur?.Weeks ?? 0) + (week.TotalFame > 0 ? 1 : 0));
        }

        return agg.Values.Where(r => r.Fame > 0).ToList();
    }
}
