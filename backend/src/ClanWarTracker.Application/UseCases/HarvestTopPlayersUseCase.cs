using System.Text.Json;
using ClanWarTracker.Application.Meta;
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
    IMetaRepository meta,
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
    /// <param name="Meta">Итог сбора меты по боям: сколько боёв и колод вошло или почему нет.</param>
    public record LastHarvest(DateTime AtUtc, int Rows, string? Problem, string? Meta = null);

    /// <summary>Потолок самого API: больше тысячи мест rankings не отдаёт.</summary>
    public const int TopSize = 1000;

    /// <summary>
    /// Сколько профилей тянем одновременно. Пять — заметно ниже лимита токена
    /// (порядка десяти в секунду) и оставляет запас живым запросам из приложения,
    /// которые идут через тот же ключ.
    /// </summary>
    private const int Parallelism = 5;

    /// <summary>
    /// Чьи журналы боёв читаем для меты. Не вся тысяча: это ещё тысяча запросов
    /// поверх профилей, а сбор из панели идёт внутри HTTP-запроса. Пятьсот журналов
    /// по 25 боёв - больше десяти тысяч боёв в день, для процентов побед хватает.
    /// </summary>
    public const int MetaPlayers = 500;

    /// <summary>Сколько дней меты храним. Витрина считает окно в неделю.</summary>
    public const int MetaKeepDays = 7;

    /// <summary>
    /// Колоду, сыгранную за день один раз, не храним: она ничего не скажет ни о
    /// проценте побед, ни о популярности, а таких колод большинство.
    /// </summary>
    private const int MinDeckGamesPerDay = 2;

    /// <param name="Rows">Сколько строк записано.</param>
    /// <param name="Skipped">
    /// Почему снимка не вышло. null — вышел. «Уже есть за сегодня» — тоже причина,
    /// но штатная, поэтому она отделена флагом <paramref name="AlreadyDone"/>.
    /// </param>
    public record HarvestResult(int Rows, string? Skipped, bool AlreadyDone = false, string? Meta = null)
    {
        public static readonly HarvestResult Done = new(0, "снимок за сегодня уже есть", AlreadyDone: true);
    }

    public async Task<HarvestResult> ExecuteAsync(bool force = false, CancellationToken ct = default)
    {
        try
        {
            return await CollectAsync(force, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Без этого упавший сбор оставлял в панели причину прошлой попытки,
            // и казалось, что кнопка вообще ничего не делает.
            return await RememberAsync(new HarvestResult(0, $"сбор упал: {ex.GetType().Name}: {ex.Message}"), ct);
        }
    }

    private async Task<HarvestResult> CollectAsync(bool force, CancellationToken ct)
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
        var logs = new List<CrRecentBattle>?[ranking.Count];
        var logFailures = 0;
        using var gate = new SemaphoreSlim(Parallelism);

        await Task.WhenAll(ranking.Select(async (r, i) =>
        {
            await gate.WaitAsync(ct);
            try
            {
                CrPlayerInfo? info = null;
                try { info = await crApi.GetPlayerInfoAsync(r.Tag, ct); }
                catch { /* закрытый или пропавший профиль — строку всё равно пишем */ }

                // Журнал нужен для меты (первые MetaPlayers) и для колоды, когда
                // профиль отдал её неполной: у части игроков «текущая колода» в API
                // приходит из 7, 6 и даже одной карты.
                var profileDeck = info?.CurrentDeck ?? [];
                if (i < MetaPlayers || profileDeck.Count < 8)
                {
                    try { logs[i] = await crApi.GetRecentBattlesAsync(r.Tag, ct); }
                    catch { if (i < MetaPlayers) Interlocked.Increment(ref logFailures); }
                }

                // Колода последнего настоящего боя - всегда восемь карт и то, чем
                // игрок реально играет сейчас. Профиль - запасной вариант.
                var lastBattleDeck = logs[i]?
                    .Where(CollectPlayerBattlesUseCase.Counts)
                    .OrderByDescending(b => b.BattleTimeUtc)
                    .Select(b => b.MyDeck)
                    .FirstOrDefault();

                rows[i] = new TopPlayer
                {
                    DayUtc = day,
                    Rank = r.Rank,
                    PlayerTag = r.Tag,
                    Name = info?.Name ?? r.Name,
                    ClanName = info?.ClanName ?? r.ClanName,
                    // Рейтинг Пути легенд (те самые 3000+) — из профиля, если он там есть.
                    Trophies = info?.CurrentPathOfLegend?.Trophies is int pol and > 0
                        ? pol
                        : r.Trophies > 0 ? r.Trophies : info?.Trophies ?? 0,
                    ExpLevel = info?.ExpLevel ?? 0,
                    DeckCardIds = Pack(lastBattleDeck ?? profileDeck),
                };
            }
            finally { gate.Release(); }
        }));

        await top.ReplaceDayAsync(day, rows, ct);
        var metaNote = await SaveMetaAsync(day, logs, logFailures, ct);
        if (!string.IsNullOrEmpty(response.Source)) metaNote = $"источник: {response.Source}\n{metaNote}";
        return await RememberAsync(new HarvestResult(rows.Length, null, Meta: metaNote), ct);
    }

    /// <summary>
    /// Мета дня из журналов боёв. Отдельно от снимка мест: сломайся она, места и
    /// колоды профилей уже записаны, и витрина топа работает как раньше.
    /// </summary>
    private async Task<string> SaveMetaAsync(
        string day, List<CrRecentBattle>?[] logs, int failures, CancellationToken ct)
    {
        try
        {
            // Только последние сутки: снимок раз в день, и бой, попавший во вчерашний
            // снимок, в сегодняшнем посчитался бы второй раз.
            var since = DateTime.UtcNow.AddHours(-24);
            var battles = logs
                .Take(MetaPlayers)
                .Where(l => l is not null)
                .SelectMany(l => l!)
                .Where(b => b.BattleTimeUtc >= since);

            var result = MetaAggregator.Build(battles);

            var decks = result.Decks
                .Where(kv => kv.Value.Games >= MinDeckGamesPerDay)
                .Select(kv => new MetaDeckDay
                {
                    DayUtc = day,
                    DeckKey = kv.Key,
                    Games = kv.Value.Games,
                    Wins = kv.Value.Wins,
                    Draws = kv.Value.Draws,
                })
                .ToList();

            var matchups = result.Matchups
                .Select(kv => new MetaMatchupDay
                {
                    DayUtc = day,
                    CardKey = kv.Key.Card,
                    OppCardKey = kv.Key.Opp,
                    Games = kv.Value.Games,
                    Wins = kv.Value.Wins,
                })
                .ToList();

            var keepFrom = DateOnly.Parse(day).AddDays(-(MetaKeepDays - 1)).ToString("yyyy-MM-dd");
            await meta.ReplaceDayAsync(day, decks, matchups, keepFrom, ct);

            var read = logs.Take(MetaPlayers).Count(l => l is not null);
            return $"мета: журналов {read}, не открылось {failures}, боёв {result.Battles}, " +
                   $"колод {decks.Count}, пар карт {matchups.Count}";
        }
        catch (Exception ex)
        {
            return $"мета не собралась: {ex.GetType().Name}: {ex.Message}";
        }
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
            var record = new LastHarvest(DateTime.UtcNow, result.Rows, result.Skipped, result.Meta);
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
