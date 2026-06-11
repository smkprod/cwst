using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayerController(IPlayerRepository players, GetPlayerStatsUseCase getStats) : ControllerBase
{
    /// <summary>GET /api/players/me — текущий привязанный игрок.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        return player is null
            ? NotFound(new { error = "player_not_linked" })
            : Ok(new { player.PlayerTag, player.Name });
    }

    /// <summary>GET /api/players/me/stats — детальная статистика по текущему игроку.</summary>
    [HttpGet("me/stats")]
    public async Task<IActionResult> MyStats(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var stats = await getStats.ExecuteAsync(userId, ct);
        if (stats is null)
        {
            // Различаем "не привязан" и "не нашли в текущей войне"
            var player = await players.GetByTelegramIdAsync(userId, ct);
            return player is null
                ? NotFound(new { error = "player_not_linked" })
                : NotFound(new { error = "not_in_current_war", message = "Игрока нет в текущем составе войны" });
        }
        return Ok(stats);
    }
}
