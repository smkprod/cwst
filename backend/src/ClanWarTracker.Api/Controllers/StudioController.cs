using ClanWarTracker.Application.Security;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>«Студия» блогера: его турниры и ссылки на виджеты для OBS.</summary>
[ApiController]
[Route("api/studio")]
public class StudioController(StudioUseCase studio, ServiceAccess access) : ControllerBase
{
    /// <summary>GET /api/studio — личный ключ виджетов и свои турниры с их ключами.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        return Ok(await studio.GetAsync(UserId, ct));
    }

    /// <summary>POST /api/studio/rotate — новый личный ключ, если старые ссылки утекли.</summary>
    [HttpPost("rotate")]
    public async Task<IActionResult> Rotate(CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        await studio.RotateAsync(UserId, ct);
        return Ok(await studio.GetAsync(UserId, ct));
    }

    public record ChallengeRequest(string? Title, string? Prize, DateTime StartUtc, DateTime EndUtc, string? Rule = null);

    /// <summary>POST /api/studio/challenges — новый челлендж для своих зрителей.</summary>
    [HttpPost("challenges")]
    public async Task<IActionResult> CreateChallenge([FromBody] ChallengeRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        return await Done(studio.CreateChallengeAsync(UserId, req.Title, req.Prize, req.StartUtc, req.EndUtc, req.Rule, ct), ct);
    }

    /// <summary>PUT /api/studio/challenges/{code} — поправить название, приз или время.</summary>
    [HttpPut("challenges/{code}")]
    public async Task<IActionResult> UpdateChallenge(string code, [FromBody] ChallengeRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        return await Done(studio.UpdateChallengeAsync(UserId, code, req.Title, req.Prize, req.StartUtc, req.EndUtc, req.Rule, ct), ct);
    }

    /// <summary>DELETE /api/studio/challenges/{code}.</summary>
    [HttpDelete("challenges/{code}")]
    public async Task<IActionResult> DeleteChallenge(string code, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        return await Done(studio.DeleteChallengeAsync(UserId, code, ct), ct);
    }

    /// <summary>Ошибка - понятным кодом, успех - свежая Студия целиком.</summary>
    private async Task<IActionResult> Done(Task<StudioUseCase.ChallengeError?> action, CancellationToken ct) =>
        await action switch
        {
            StudioUseCase.ChallengeError.BadTitle => BadRequest(new { error = "bad_title" }),
            StudioUseCase.ChallengeError.BadDates => BadRequest(new { error = "bad_dates" }),
            StudioUseCase.ChallengeError.TooMany => StatusCode(409, new { error = "too_many" }),
            StudioUseCase.ChallengeError.NotFound => NotFound(new { error = "not_found" }),
            StudioUseCase.ChallengeError.NotYours => StatusCode(403, new { error = "not_yours" }),
            _ => Ok(await studio.GetAsync(UserId, ct)),
        };

    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    private async Task<IActionResult?> DenyAsync(CancellationToken ct)
    {
        var me = await access.ResolveAsync(UserId, HttpContext.Items["TelegramUsername"] as string, ct);
        return (me.Permissions & ServicePermission.Creator) == ServicePermission.Creator
            ? null
            : StatusCode(403, new { error = "not_creator" });
    }
}
