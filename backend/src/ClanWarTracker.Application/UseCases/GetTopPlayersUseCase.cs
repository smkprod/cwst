using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Список мирового топа из последнего снимка, с колодами.</summary>
public class GetTopPlayersUseCase(IClashRoyaleApi crApi, ITopPlayerRepository top)
{
    public async Task<List<TopPlayerRowDto>> ExecuteAsync(int skip, int take, CancellationToken ct = default)
    {
        var day = await top.LatestDayAsync(ct);
        if (day is null) return [];

        var rows = await top.GetDayAsync(day, ct);
        // Постранично: тысяча строк с восемью иконками каждая — это мегабайты JSON
        // и секунды отрисовки на телефоне.
        var page = rows.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100)).ToList();
        if (page.Count == 0) return [];

        Dictionary<int, CrCatalogCard> catalog;
        try
        {
            var all = await crApi.GetAllCardsAsync(ct);
            catalog = all.Values.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        }
        catch { catalog = []; }

        return page.Select(r => new TopPlayerRowDto(
            r.Rank, r.PlayerTag, r.Name, r.ClanName, r.Trophies, r.ExpLevel,
            (r.DeckCardIds ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(id => id != 0)
                .Select(id =>
                {
                    var card = catalog.GetValueOrDefault(id);
                    return new TopDeckCardDto(id, card?.Name ?? $"#{id}", card?.IconUrl ?? "");
                })
                .ToList()))
            .ToList();
    }
}
