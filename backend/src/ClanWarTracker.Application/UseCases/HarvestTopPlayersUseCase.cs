using System.Text.Json;
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
public class HarvestTopPlayersUseCase(
    IClashRoyaleApi crApi,
    ITopPlayerRepository top,
    IServiceSettingRepository settings)
{
    /// <summary>
    /// Куда кладём исход последней попытки.
    ///
    /// В настройки, а не в поле класса, потому что собирает снимок воркер, а
    /// смотрит панель — API, и это разные контейнеры с разной памятью. Пока исход
    /// жил в поле, единственным способом узнать причину был вход на сервер за
    /// логами; на деле про поломку узнавали от человека, открывшего вкладку.
    /// </summary>
    public const string LastHarvestKey = "top.lastHarvest";

    /// <param name="Rows">Сколько строк записала последняя попытка.</param>
    /// <param name="Problem">Причина, если снимка не вышло. null — вышел.</param>
    public record LastHarvest(DateTime AtUtc, int Rows, string? Problem);

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
            return await RememberAsync(new HarvestResult(0, response.Problem ?? "рейтинг пуст без объяснения"), ct);

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
        return await RememberAsync(new HarvestResult(rows.Length, null), ct);
    }

    /// <summary>
    /// Запоминает исход попытки, чтобы панель могла его показать.
    ///
    /// Ошибка записи не портит результат: снимок уже в базе, и терять его из-за
    /// того, что не сохранилась строчка диагностики, — обмен не в ту сторону.
    /// </summary>
    private async Task<HarvestResult> RememberAsync(HarvestResult result, CancellationToken ct)
    {
        try
        {
            var record = new LastHarvest(DateTime.UtcNow, result.Rows, result.Skipped);
            await settings.SetAsync(LastHarvestKey, JsonSerializer.Serialize(record), ct);
        }
        catch { /* диагностика не обязана работать, чтобы работал сбор */ }
        return result;
    }

    /// <summary>Исход последней попытки. null — попыток ещё не было или запись не читается.</summary>
    public async Task<LastHarvest?> LastAsync(CancellationToken ct = default)
    {
        var raw = await settings.GetAsync(LastHarvestKey, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return JsonSerializer.Deserialize<LastHarvest>(raw); }
        catch { return null; }
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
