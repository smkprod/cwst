using ClanWarTracker.Application.Analytics;
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
public class TrackController(IAppOpenRepository opens, IPlayerRepository players) : ControllerBase
{
    public record OpenRequest(string? Param);

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
