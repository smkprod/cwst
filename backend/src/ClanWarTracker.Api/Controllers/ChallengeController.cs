using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>Уикенд-челлендж и челленджи блогеров: таблица и вступление.</summary>
[ApiController]
[Route("api/challenge")]
public class ChallengeController(ChallengeUseCase challenge) : ControllerBase
{
    /// <summary>GET /api/challenge?code= — событие, своя строка и таблица. Без кода — общий челлендж.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? code, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var dto = await challenge.GetAsync(userId, code, ct);
        return dto is null ? NotFound(new { error = "challenge_not_found" }) : Ok(dto);
    }

    /// <summary>POST /api/challenge/join?code= — присоединиться. Нужен привязанный тег.</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromQuery] string? code, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return await challenge.JoinAsync(userId, code, ct) switch
        {
            ChallengeUseCase.JoinOutcome.NotLinked => NotFound(new { error = "player_not_linked" }),
            ChallengeUseCase.JoinOutcome.NotFound => NotFound(new { error = "challenge_not_found" }),
            ChallengeUseCase.JoinOutcome.Ended => StatusCode(409, new { error = "challenge_ended" }),
            _ => Ok(await challenge.GetAsync(userId, code, ct)),
        };
    }
}
