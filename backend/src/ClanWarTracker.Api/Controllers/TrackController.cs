using ClanWarTracker.Application.Analytics;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Откуда открыли Mini App. Фронтенд шлёт сырой параметр запуска один раз за открытие;
/// источник и раздел сервер вычисляет сам (OpenSource) - подделать можно разве что свою
/// же строчку в статистике.
/// </summary>
[ApiController]
[Route("api/track")]
public class TrackController(IAppOpenRepository opens, IPlayerRepository players, IFunnelRepository funnel) : ControllerBase
{
    public record OpenRequest(string? Param);

    public record OfferRequest(string? Product, string? Place);

    /// <summary>
    /// POST /api/track/offer — человек увидел предложение (окно покупки, витрину). Только
    /// этот шаг идёт от приложения: «нажал купить» сервер отмечает сам, выставляя счёт.
    /// </summary>
    [HttpPost("offer")]
    public async Task<IActionResult> Offer([FromBody] OfferRequest req, CancellationToken ct)
    {
        if (HttpContext.Items["TelegramUserId"] is not long userId) return Unauthorized();
        if (Array.IndexOf(FunnelEvent.Products.All, req.Product) < 0 || Array.IndexOf(FunnelEvent.SeenPlaces, req.Place) < 0)
            return BadRequest(new { error = "bad_offer" });
        await PurchaseFunnel.MarkAsync(funnel, userId, req.Product!, FunnelEvent.Steps.Seen, req.Place!, ct);
        return Ok(new { ok = true });
    }

    /// <summary>POST /api/track/open — заход в приложение.</summary>
    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenRequest req, CancellationToken ct)
    {
        if (HttpContext.Items["TelegramUserId"] is not long userId) return Unauthorized();
        var (source, dest) = OpenSource.Parse(req.Param);
        var linked = await players.GetByTelegramIdAsync(userId, ct) is not null;
        await opens.AddAsync(new AppOpen
        {
            TelegramUserId = userId,
            DayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Source = source,
            Dest = dest,
            Linked = linked,
            FirstUtc = DateTime.UtcNow,
        }, ct);
        return Ok(new { ok = true });
    }
}
