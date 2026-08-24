using ClanWarTracker.Api.Rendering;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Картинки для inline-режима.
///
/// Ручка намеренно БЕЗ авторизации (см. исключение в TelegramAuthMiddleware): картинку
/// скачивает сам Telegram со своих серверов, и никакого initData у него нет. Отдаём мы
/// при этом только то, что и так открыто в публичном профиле игрока Clash Royale —
/// имя, клан и медали за неделю.
///
/// Готовая картинка кэшируется: без этого один пересланный в большой чат результат
/// заставил бы рисовать её на каждый показ превью.
/// </summary>
[ApiController]
[Route("api/img")]
public class ImageController(
    IClashRoyaleApi crApi,
    GetClanStatusUseCase getStatus,
    WarCardRenderer renderer,
    ITelegramBotClient bot,
    IMemoryCache cache) : ControllerBase
{
    /// <summary>Столько живёт нарисованная карточка. Война меняется медленнее.</summary>
    private static readonly TimeSpan CardTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// GET /api/img/war/{tag}.jpg — карточка «моя война» для пересылки в чат.
    /// Именно JPEG: для inline-фото Telegram принимает только его.
    /// </summary>
    [HttpGet("war/{tag}.jpg")]
    public async Task<IActionResult> WarCard(string tag, CancellationToken ct)
    {
        var playerTag = "#" + tag.TrimStart('#').ToUpperInvariant();

        var jpeg = await cache.GetOrCreateAsync($"warcard:{playerTag}", async entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = CardTtl;
            return await BuildAsync(playerTag, ct);
        });

        if (jpeg is null) return NotFound();

        // Telegram перекачивает картинку сам и уважает Cache-Control — пусть не дёргает
        // нас на каждый показ превью в чате.
        Response.Headers.CacheControl = $"public, max-age={(int)CardTtl.TotalSeconds}";
        return File(jpeg, "image/jpeg");
    }

    private async Task<byte[]?> BuildAsync(string playerTag, CancellationToken ct)
    {
        string? clanTag;
        try { clanTag = await crApi.GetPlayerClanTagAsync(playerTag, ct); }
        catch { return null; }
        if (clanTag is null) return null;

        var status = await getStatus.ExecuteAsync(clanTag, ct);
        var me = status?.Players.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));
        if (status is null || me is null) return null;

        var ours = status.Race.FirstOrDefault(r => r.IsOurClan);

        var botName = await cache.GetOrCreateAsync("botusername", async e =>
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

        return renderer.Render(new WarCardModel(
            PlayerName: me.Name,
            ClanName: status.ClanName,
            Fame: me.Fame,
            Rank: me.Rank,
            ClanSize: status.Players.Count,
            DecksToday: me.DecksUsedToday,
            RacePosition: ours?.Position ?? 0,
            RaceClans: status.Race.Count,
            BotName: botName ?? "bot"));
    }
}
