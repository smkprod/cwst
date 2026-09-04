using ClanWarTracker.Api.Rendering;
using ClanWarTracker.Application.Games;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Картинки для inline-режима.
///
/// Ручки намеренно БЕЗ авторизации (см. исключение в TelegramAuthMiddleware): картинку
/// скачивает сам Telegram со своих серверов, и никакого initData у него нет. Отдаём мы
/// при этом только то, что и так открыто в публичном профиле игрока Clash Royale.
///
/// Готовая картинка кэшируется: без этого один пересланный в большой чат результат
/// заставил бы рисовать её на каждый показ превью.
/// </summary>
[ApiController]
[Route("api/img")]
public class ImageController(
    IClashRoyaleApi crApi,
    GetClanStatusUseCase getStatus,
    CardRenderer renderer,
    ITelegramBotClient bot,
    IPuzzleRepository puzzles,
    IPuzzleSecret puzzleSecret,
    IMemoryCache cache) : ControllerBase
{
    /// <summary>Столько живёт нарисованная карточка. Война меняется медленнее.</summary>
    private static readonly TimeSpan CardTtl = TimeSpan.FromMinutes(10);

    /// <summary>Профиль и клан меняются ещё медленнее войны.</summary>
    private static readonly TimeSpan SlowTtl = TimeSpan.FromMinutes(30);

    /// <summary>GET /api/img/war/{tag}.jpg — карточка «моя война».</summary>
    [HttpGet("war/{tag}.jpg")]
    public Task<IActionResult> WarCard(string tag, CancellationToken ct) =>
        Serve($"war:{tag}", CardTtl, async () =>
        {
            var playerTag = Normalize(tag);
            string? clanTag;
            try { clanTag = await crApi.GetPlayerClanTagAsync(playerTag, ct); }
            catch { return null; }
            if (clanTag is null) return null;

            var status = await getStatus.ExecuteAsync(clanTag, ct);
            var me = status?.Players.FirstOrDefault(p =>
                string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
            if (status is null || me is null) return null;

            var ours = status.Race.FirstOrDefault(r => r.IsOurClan);
            return renderer.RenderWar(new WarCardModel(
                me.Name, status.ClanName, me.Fame, me.Rank, status.Players.Count,
                me.DecksUsedToday, ours?.Position ?? 0, status.Race.Count,
                await BotNameAsync(ct), await ArtAsync(playerTag, ct)));
        });

    /// <summary>GET /api/img/profile/{tag}.jpg — карточка игрока.</summary>
    [HttpGet("profile/{tag}.jpg")]
    public Task<IActionResult> ProfileCard(string tag, CancellationToken ct) =>
        Serve($"profile:{tag}", SlowTtl, async () =>
        {
            var info = await Try(() => crApi.GetPlayerInfoAsync(Normalize(tag), ct));
            return info is null ? null : renderer.RenderProfile(new ProfileCardModel(
                info.Name, info.ClanName, info.ExpLevel, info.Trophies, info.BestTrophies,
                info.WarDayWins, info.ThreeCrownWins,
                await BotNameAsync(ct), await ArtAsync(info.Tag, ct)));
        });

    /// <summary>GET /api/img/clan/{tag}.jpg — карточка клана.</summary>
    [HttpGet("clan/{tag}.jpg")]
    public Task<IActionResult> ClanCard(string tag, CancellationToken ct) =>
        Serve($"clan:{tag}", SlowTtl, async () =>
        {
            var info = await Try(() => crApi.GetClanInfoAsync(Normalize(tag), ct));
            return info is null ? null : renderer.RenderClan(new ClanCardModel(
                info.Name, info.Tag, info.MemberCount, info.ClanScore,
                info.ClanWarTrophies, info.RequiredTrophies, await BotNameAsync(ct), null));
        });

    /// <summary>GET /api/img/deck/{tag}.jpg — текущая колода игрока настоящими картами.</summary>
    [HttpGet("deck/{tag}.jpg")]
    public Task<IActionResult> DeckCard(string tag, CancellationToken ct) =>
        Serve($"deck:{tag}", SlowTtl, async () =>
        {
            var info = await Try(() => crApi.GetPlayerInfoAsync(Normalize(tag), ct));
            if (info is null || info.CurrentDeck.Count == 0) return null;

            var cards = info.CurrentDeck
                .Select(c => new DeckCardEntry(
                    c.Name,
                    // Открыл эволюцию — показываем её арт, как в игре
                    c.EvolutionLevel > 0 && c.EvoIconUrl is not null ? c.EvoIconUrl : c.IconUrl,
                    c.Level,
                    c.Level >= c.MaxLevel))
                .ToList();

            return renderer.RenderDeck(new DeckCardModel(
                $"Колода {info.Name}", info.ClanName ?? "без клана", cards,
                Math.Round(info.CurrentDeck.Average(c => (double)c.Level), 1),
                await BotNameAsync(ct)));
        });

    /// <summary>
    /// GET /api/img/puzzle/{token}.jpg — фрагмент карты дня.
    ///
    /// Без авторизации, как и остальные картинки: их грузит &lt;img&gt;, а заголовок с
    /// initData туда не подставить. Поэтому в адресе подписанная пара «игрок и день»,
    /// а НЕ уровень приближения: будь там уровень, любой запросил бы сразу третий,
    /// увидел почти весь арт и ответил с первой попытки на три очка.
    ///
    /// Уровень сервер берёт из базы — из того, сколько попыток человек уже потратил.
    /// </summary>
    [HttpGet("puzzle/{token}.jpg")]
    public async Task<IActionResult> PuzzleFragment(string token, CancellationToken ct)
    {
        var pass = PuzzleToken.Verify(token, puzzleSecret.Value);
        if (pass is not { } id) return NotFound();

        // Загадка сменилась, а страница осталась открытой со вчерашним адресом —
        // отдавать вчерашний фрагмент нельзя, он уже ничей.
        var today = DailyCard.DayNumber(DateTime.UtcNow);
        if (id.Day != today) return NotFound();

        var played = await puzzles.GetAsync(id.PlayerId, today, ct);
        var level = played is null
            ? 1
            : played.Solved || played.Attempts >= DailyPuzzleUseCase.MaxAttempts
                ? DailyPuzzleUseCase.MaxAttempts        // доиграл — показываем всё
                : played.Attempts + 1;

        // Кэш общий для всех на одном уровне: картинка зависит от дня и уровня,
        // а не от игрока, и рисовать её каждому заново незачем.
        return await Serve($"puzzle:{today}:{level}", CardTtl, async () =>
        {
            var catalog = await Try(() => crApi.GetAllCardsAsync(ct));
            if (catalog is null) return null;

            var card = DailyCard.Pick(catalog.Values.ToList(), today);
            return card is null ? null : renderer.RenderPuzzle(card.IconUrl, level, DailyCard.Seed($"day:{today}"));
        });
    }

    /// <summary>
    /// GET /api/img/puzzle/sample/{seed}.jpg — лист предпросмотра для настройки игры.
    ///
    /// Карты берутся случайные по seed, а НЕ карты ближайших дней: иначе эта ручка
    /// раздавала бы ответы на будущие загадки всем желающим.
    /// </summary>
    [HttpGet("puzzle/sample/{seed:int}.jpg")]
    public Task<IActionResult> PuzzleSample(int seed, CancellationToken ct) =>
        Serve($"puzzlesample:{seed}", SlowTtl, async () =>
        {
            var catalog = await Try(() => crApi.GetAllCardsAsync(ct));
            if (catalog is null) return null;

            var all = catalog.Values.OrderBy(c => c.Id).ToList();
            if (all.Count == 0) return null;

            var rnd = new Random(seed);
            var cards = Enumerable.Range(0, 4)
                .Select(_ => all[rnd.Next(all.Count)])
                .Select(c => (c.Name, c.IconUrl))
                .ToList();

            return renderer.RenderPuzzleSheet(cards, seed);
        });

    /// <summary>
    /// Общая обвязка: кэш готовой картинки, заголовки и честный 404, когда рисовать нечего.
    /// </summary>
    private async Task<IActionResult> Serve(string key, TimeSpan ttl, Func<Task<byte[]?>> build)
    {
        var jpeg = await cache.GetOrCreateAsync($"card:{key}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = ttl;
            try { return await build(); }
            catch
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return null;
            }
        });

        if (jpeg is null) return NotFound();

        // Telegram перекачивает картинку сам и уважает Cache-Control — пусть не дёргает
        // нас на каждый показ превью в чате.
        Response.Headers.CacheControl = $"public, max-age={(int)ttl.TotalSeconds}";
        return File(jpeg, "image/jpeg");
    }

    private static string Normalize(string tag) => "#" + tag.TrimStart('#').ToUpperInvariant();

    /// <summary>
    /// Арт для фона карточки — любимая карта игрока, а если её нет, первая из колоды.
    /// Именно она делает карточку похожей на игру, а не на тёмный прямоугольник с цифрами.
    /// null — рисуем без арта, композиция это переживает.
    /// </summary>
    private async Task<string?> ArtAsync(string playerTag, CancellationToken ct)
    {
        var info = await Try(() => crApi.GetPlayerInfoAsync(playerTag, ct));
        if (info is null) return null;

        if (info.CurrentFavouriteCard is { } fav)
        {
            var catalog = await Try(() => crApi.GetAllCardsAsync(ct));
            if (catalog is not null && catalog.TryGetValue(fav, out var card))
                return card.IconUrl;
        }
        return info.CurrentDeck.FirstOrDefault()?.IconUrl;
    }

    private static async Task<T?> Try<T>(Func<Task<T?>> get) where T : class
    {
        try { return await get(); }
        catch { return null; }
    }

    private async Task<string> BotNameAsync(CancellationToken ct)
    {
        var name = await cache.GetOrCreateAsync("botusername", async e =>
        {
            e.Size = 1;
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            try { return (await bot.GetMe(ct)).Username; }
            catch
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return null;
            }
        });
        return name ?? "bot";
    }
}
