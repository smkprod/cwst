using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>Уикенд-челлендж: таблица и вступление.</summary>
[ApiController]
[Route("api/challenge")]
public class ChallengeController(ChallengeUseCase challenge) : ControllerBase
{
    /// <summary>GET /api/challenge — событие, своя строка и таблица.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return Ok(await challenge.GetAsync(userId, ct));
    }

    /// <summary>POST /api/challenge/join — присоединиться. Нужен привязанный тег.</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return await challenge.JoinAsync(userId, ct) switch
        {
            ChallengeUseCase.JoinOutcome.NotLinked => NotFound(new { error = "player_not_linked" }),
            ChallengeUseCase.JoinOutcome.Ended => StatusCode(409, new { error = "challenge_ended" }),
            _ => Ok(await challenge.GetAsync(userId, ct)),
        };
    }
}
