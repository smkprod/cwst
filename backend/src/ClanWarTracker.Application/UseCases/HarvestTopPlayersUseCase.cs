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

    /// <returns>Сколько строк записано. 0 — снимок за сегодня уже есть или рейтинг не пришёл.</returns>
    public async Task<int> ExecuteAsync(bool force = false, CancellationToken ct = default)
    {
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!force && await top.HasDayAsync(day, ct)) return 0;

        var ranking = await crApi.GetGlobalRankingAsync(TopSize, ct);
        if (ranking.Count == 0) return 0;

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
        return rows.Length;
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
