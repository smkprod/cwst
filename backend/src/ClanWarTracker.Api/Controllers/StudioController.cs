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

    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    private async Task<IActionResult?> DenyAsync(CancellationToken ct)
    {
        var me = await access.ResolveAsync(UserId, HttpContext.Items["TelegramUsername"] as string, ct);
        return (me.Permissions & ServicePermission.Creator) == ServicePermission.Creator
            ? null
            : StatusCode(403, new { error = "not_creator" });
    }
}
