using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Мета мирового топа: чем играет тысяча лучших и как это менялось.
///
/// Считается по накопленным снимкам, а не по живому API: рейтинг отдаёт только
/// «сейчас», и любая динамика — «Мега-рыцарь прибавил после балансной правки» —
/// существует лишь потому, что мы храним вчерашний день.
/// </summary>
public class GetTopMetaUseCase(IClashRoyaleApi crApi, ITopPlayerRepository top)
{
    /// <summary>С чем сравниваем «стало» — со снимком недельной давности.</summary>
    private const int CompareDaysBack = 7;

    /// <summary>Сколько карт показываем в витрине популярности.</summary>
    private const int TopCards = 24;

    public async Task<TopMetaDto?> ExecuteAsync(CancellationToken ct = default)
    {
        var latestDay = await top.LatestDayAsync(ct);
        if (latestDay is null) return null;

        var today = await top.GetDayAsync(latestDay, ct);
        if (today.Count == 0) return null;

        // Снимок недельной давности: берём ближайший к цели из того, что есть.
        // Точного дня может не быть — бот мог не работать или начать позже.
        var days = await top.DaysAsync(40, ct);
        var target = DateOnly.Parse(latestDay).AddDays(-CompareDaysBack).ToString("yyyy-MM-dd");
        var pastDay = days.Where(d => string.CompareOrdinal(d, target) <= 0).FirstOrDefault();
        var past = pastDay is null ? [] : await top.GetDayAsync(pastDay, ct);

        var catalog = await SafeCatalogAsync(ct);

        var nowShare = Share(today);
        var pastShare = Share(past);
        var withDeck = today.Count(p => p.DeckCardIds is not null);

        var cards = nowShare
            .OrderByDescending(kv => kv.Value)
            .Take(TopCards)
            .Select(kv =>
            {
                var card = catalog.GetValueOrDefault(kv.Key);
                return new TopCardDto(
                    CardId: kv.Key,
                    Name: card?.Name ?? $"#{kv.Key}",
                    IconUrl: card?.IconUrl ?? "",
                    // Доля от тех, у кого колода вообще известна, а не от всей тысячи:
                    // закрытые профили иначе занижали бы каждую карту одинаково,
                    // и проценты перестали бы складываться во что-то осмысленное.
                    Percent: Math.Round(kv.Value * 100, 1),
                    DeltaPercent: pastShare.Count == 0
                        ? null
                        : Math.Round((kv.Value - pastShare.GetValueOrDefault(kv.Key)) * 100, 1));
            })
            .ToList();

        return new TopMetaDto(
            DayUtc: latestDay,
            ComparedToDayUtc: pastDay,
            PlayersTracked: today.Count,
            PlayersWithDeck: withDeck,
            // Порог входа: кубки последнего места в снимке. Это и есть ответ на
            // «сколько нужно, чтобы попасть в топ-1000».
            CutoffTrophies: today[^1].Trophies,
            TopTrophies: today[0].Trophies,
            AvgLevel: withDeck == 0
                ? 0
                : Math.Round(today.Where(p => p.ExpLevel > 0).DefaultIfEmpty()
                    .Average(p => p?.ExpLevel ?? 0), 1),
            Cards: cards);
    }

    /// <summary>Доля колод, где встречается карта: id карты → 0..1.</summary>
    private static Dictionary<int, double> Share(List<TopPlayer> rows)
    {
        var decks = rows.Where(r => r.DeckCardIds is not null).ToList();
        if (decks.Count == 0) return [];

        var counts = new Dictionary<int, int>();
        foreach (var row in decks)
            foreach (var part in row.DeckCardIds!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out var id))
                    counts[id] = counts.GetValueOrDefault(id) + 1;

        return counts.ToDictionary(kv => kv.Key, kv => (double)kv.Value / decks.Count);
    }

    /// <summary>
    /// Справочник карт нужен только для имён и иконок. Не пришёл — показываем
    /// проценты с номерами вместо названий: цифры полезнее пустого экрана.
    /// </summary>
    private async Task<Dictionary<int, CrCatalogCard>> SafeCatalogAsync(CancellationToken ct)
    {
        try
        {
            var catalog = await crApi.GetAllCardsAsync(ct);
            return catalog.Values.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        }
        catch
        {
            return [];
        }
    }
}
