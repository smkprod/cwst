using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>«Охота на тыквы»: свои находки, общий счёт и находка тыквы.</summary>
[ApiController]
[Route("api/pumpkins")]
public class PumpkinController(PumpkinHuntUseCase hunt) : ControllerBase
{
    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    /// <summary>GET /api/pumpkins — открыта ли охота, что уже найдено, сколько нашли все.</summary>
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await hunt.StatusAsync(UserId, ct));

    /// <summary>POST /api/pumpkins/{id} — тыква найдена. За все 13 - Плюс.</summary>
    [HttpPost("{id:int}")]
    public async Task<IActionResult> Find(int id, CancellationToken ct) =>
        await hunt.FindAsync(UserId, id, ct) is { } r ? Ok(r) : BadRequest(new { error = "hunt_closed" });
}
