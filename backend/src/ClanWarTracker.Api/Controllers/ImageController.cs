using ClanWarTracker.Api.Rendering;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Enums;
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
    ITournamentRepository tournaments,
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
    /// GET /api/img/perfect/{tag}.jpg — поздравление с идеальным днём для чата.
    ///
    /// Число всегда 900: это и есть определение идеального дня, а не показатель,
    /// который надо откуда-то доставать.
    /// </summary>
    [HttpGet("perfect/{tag}.jpg")]
    public Task<IActionResult> PerfectDayCard(string tag, CancellationToken ct) =>
        Serve($"perfect:{tag}", CardTtl, async () =>
        {
            var playerTag = Normalize(tag);
            var info = await Try(() => crApi.GetPlayerInfoAsync(playerTag, ct));
            if (info is null) return null;

            return renderer.RenderPerfectDay(new PerfectDayCardModel(
                info.Name, info.ClanName ?? "без клана", 900,
                await BotNameAsync(ct), await ArtAsync(playerTag, ct)));
        });

    /// <summary>
    /// GET /api/img/achievement/{token}.jpg — карточка открытой награды спонсора.
    ///
    /// Ключ награды едет в самом сегменте пути через «~», а не отдельным
    /// параметром: адрес собирает общий помощник, который умеет только
    /// «вид/идентификатор», и городить ради одной карточки второй способ
    /// сборки адресов — дороже, чем разделить строку здесь.
    ///
    /// Без авторизации, как и остальные картинки: их качает Telegram со своих
    /// серверов. Показываем только имя, клан и название награды — всё это и так
    /// видно любому в приложении.
    /// </summary>
    [HttpGet("achievement/{token}.jpg")]
    public Task<IActionResult> AchievementCard(string token, CancellationToken ct) =>
        Serve($"achievement:{token}", CardTtl, async () =>
        {
            var parts = token.Split('~', 2);
            if (parts.Length != 2) return null;

            var playerTag = Normalize(parts[0]);
            if (!AchievementTitles.TryGetValue(parts[1], out var title)) return null;

            var info = await Try(() => crApi.GetPlayerInfoAsync(playerTag, ct));
            if (info is null) return null;

            return renderer.RenderAchievement(new AchievementCardModel(
                info.Name, info.ClanName ?? "без клана",
                title, "новая награда",
                await BotNameAsync(ct)));
        });

    /// <summary>
    /// Подписи наград. Дублируют те, что в объявлении: карточку рисует API, а
    /// объявление шлёт другой слой, и общего места у них нет — связывать их
    /// ради девяти строк значило бы тащить рисовалку в Application.
    /// </summary>
    private static readonly Dictionary<string, string> AchievementTitles = new()
    {
        ["streak"] = "Недели подряд",
        ["dailyStreak"] = "Дни подряд",
        ["perfectDays"] = "Идеальный день",
        ["mvpWeeks"] = "Лучший недели",
        ["totalFame"] = "Медали за всё время",
        ["warsPlayed"] = "Сыграно войн",
        ["perfectWeeks"] = "Идеальная неделя",
        ["perfectSeasons"] = "Идеальный сезон",
        ["boatAttacks"] = "Атаки по лодке",
    };

    /// <summary>
    /// GET /api/img/champion/{id}.jpg — итог турнира для чата.
    ///
    /// Без авторизации, как и остальные картинки: их качает Telegram со своих серверов.
    /// Отдаём только по завершённому турниру и только то, что и так видно всем в
    /// приложении, — имя команды, счёт финала и любимые карты победителей.
    /// </summary>
    [HttpGet("champion/{id:int}.jpg")]
    public Task<IActionResult> ChampionCard(int id, CancellationToken ct) =>
        Serve($"champion:{id}", SlowTtl, async () =>
        {
            var t = await tournaments.GetByIdAsync(id, ct);
            if (t is null || t.Status != TournamentStatus.Completed) return null;

            var champion = t.Participants.FirstOrDefault(x => x.FinalPlacement == 1);
            if (champion is null) return null;

            var runnerUp = t.Participants.FirstOrDefault(x => x.FinalPlacement == 2);
            var final = t.Matches
                .Where(x => x.NextMatchId is null && x.Status == TournamentMatchStatus.Completed)
                .OrderByDescending(x => x.Round)
                .FirstOrDefault();

            // Счёт со стороны чемпиона: «2:1» должно читаться как «победитель — второй».
            var score = final is null
                ? ""
                : final.WinnerParticipantId == final.ParticipantAId
                    ? $"{final.ScoreA}:{final.ScoreB}"
                    : $"{final.ScoreB}:{final.ScoreA}";

            var roster = champion.PartnerPlayerName is null
                ? ""
                : $"{champion.PlayerName} + {champion.PartnerPlayerName}";

            return renderer.RenderChampion(new ChampionCardModel(
                champion.TeamName ?? champion.PlayerName,
                roster,
                t.Name,
                score,
                runnerUp?.TeamName ?? runnerUp?.PlayerName ?? "",
                t.Participants.Count(x => x.Status != TournamentParticipantStatus.Withdrawn),
                await BotNameAsync(ct)));
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
