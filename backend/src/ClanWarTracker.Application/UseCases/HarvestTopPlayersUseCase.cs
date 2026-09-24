using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Суточный снимок мирового топа: места, кубки и колоды.
///
/// Рейтинг API отдаёт одним запросом, а вот колоды лежат в профилях — это тысяча
/// отдельных запросов. Поэтому тянем их параллельно, но с ограничителем: лимит
/// у токена считается в запросах в секунду, и выпустить тысячу разом — верный
/// способ получить 429 и остаться вообще без снимка.
///
/// Профиль, который не открылся, не отменяет строку: место и кубки берутся из
/// рейтинга, а порог входа считается именно по кубкам. Без колоды останется дыра
/// в подсчёте карт — это честнее, чем выбросить игрока целиком.
/// </summary>
public class HarvestTopPlayersUseCase(IClashRoyaleApi crApi, ITopPlayerRepository top)
{
    /// <summary>Потолок самого API: больше тысячи мест rankings не отдаёт.</summary>
    public const int TopSize = 1000;

    /// <summary>
    /// Сколько профилей тянем одновременно. Пять — заметно ниже лимита токена
    /// (порядка десяти в секунду) и оставляет запас живым запросам из приложения,
    /// которые идут через тот же ключ.
    /// </summary>
    private const int Parallelism = 5;

    /// <param name="Rows">Сколько строк записано.</param>
    /// <param name="Skipped">
    /// Почему снимка не вышло. null — вышел. «Уже есть за сегодня» — тоже причина,
    /// но штатная, поэтому она отделена флагом <paramref name="AlreadyDone"/>.
    /// </param>
    public record HarvestResult(int Rows, string? Skipped, bool AlreadyDone = false)
    {
        public static readonly HarvestResult Done = new(0, "снимок за сегодня уже есть", AlreadyDone: true);
    }

    public async Task<HarvestResult> ExecuteAsync(bool force = false, CancellationToken ct = default)
    {
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!force && await top.HasDayAsync(day, ct)) return HarvestResult.Done;

        var response = await crApi.GetGlobalRankingAsync(TopSize, ct);
        var all = response.Players;
        if (all.Count == 0)
            return new HarvestResult(0, response.Problem ?? "рейтинг пуст без объяснения");

        // Место — уникальный ключ снимка, и повтор в ответе API уронил бы вставку всей
        // тысячи разом: остались бы без снимка за день из-за одной лишней строки.
        // Тег страхуем той же логикой — один игрок дважды в рейтинге тоже бессмыслица.
        var ranking = all
            .GroupBy(r => r.Rank).Select(g => g.First())
            .GroupBy(r => r.Tag, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(r => r.Rank)
            .ToList();

        var rows = new TopPlayer[ranking.Count];
        using var gate = new SemaphoreSlim(Parallelism);

        await Task.WhenAll(ranking.Select(async (r, i) =>
        {
            await gate.WaitAsync(ct);
            try
            {
                CrPlayerInfo? info = null;
                try { info = await crApi.GetPlayerInfoAsync(r.Tag, ct); }
                catch { /* закрытый или пропавший профиль — строку всё равно пишем */ }

                rows[i] = new TopPlayer
                {
                    DayUtc = day,
                    Rank = r.Rank,
                    PlayerTag = r.Tag,
                    Name = info?.Name ?? r.Name,
                    ClanName = info?.ClanName ?? r.ClanName,
                    Trophies = r.Trophies > 0 ? r.Trophies : info?.Trophies ?? 0,
                    ExpLevel = info?.ExpLevel ?? 0,
                    DeckCardIds = Pack(info?.CurrentDeck),
                };
            }
            finally { gate.Release(); }
        }));

        await top.ReplaceDayAsync(day, rows, ct);
        return new HarvestResult(rows.Length, null);
    }

    /// <summary>
    /// Колода восемью id через запятую. Отдельная таблица на карты колоды дала бы
    /// восемь тысяч строк в день там, где хватает одной строки на игрока, а считать
    /// популярность всё равно приходится в памяти.
    /// </summary>
    private static string? Pack(List<CrDeckCard>? deck)
    {
        if (deck is null || deck.Count == 0) return null;
        return string.Join(',', deck.Select(c => c.Id));
    }
}
