using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Данные для виджетов OBS. Открыты без Telegram (OBS его не знает), доступ - по
/// секрету в ссылке. Отдаём только то, что и так видно в приложении всем: имена,
/// теги, счёт, - без Telegram-идентификаторов.
/// </summary>
[ApiController]
[Route("api/overlay")]
public class OverlayController(
    ITournamentRepository tournaments,
    StudioUseCase studio,
    DuelUseCase duels,
    ChallengeUseCase challenge) : ControllerBase
{
    /// <summary>GET /api/overlay/t/{key} — турнир целиком: сетка, участники, матчи.</summary>
    [HttpGet("t/{key}")]
    public async Task<IActionResult> Tournament(string key, CancellationToken ct)
    {
        NoCache();
        if (key.Length is < 16 or > 32) return NotFound();
        var t = await tournaments.GetByOverlayKeyAsync(key, ct);
        return t is null ? NotFound(new { error = "not_found" }) : Ok(TournamentMapping.ToDto(t, 0));
    }

    /// <summary>GET /api/overlay/s/{key}/league — топ лиги дуэлей.</summary>
    [HttpGet("s/{key}/league")]
    public async Task<IActionResult> League(string key, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        NoCache();
        if (!await studio.IsValidKeyAsync(key, ct)) return NotFound(new { error = "not_found" });
        var view = await duels.GetViewAsync(0, ct);
        return Ok(new { view.Players, Top = view.Top.Take(Math.Clamp(limit, 3, 20)), view.Recent });
    }

    /// <summary>GET /api/overlay/s/{key}/challenge — текущий челлендж и лидеры.</summary>
    [HttpGet("s/{key}/challenge")]
    public async Task<IActionResult> Challenge(string key, CancellationToken ct)
    {
        NoCache();
        if (!await studio.IsValidKeyAsync(key, ct)) return NotFound(new { error = "not_found" });
        return Ok(await challenge.GetAsync(0, ct: ct));
    }

    /// <summary>GET /api/overlay/c/{code} — челлендж блогера: код и так открыт в ссылке вступления.</summary>
    [HttpGet("c/{code}")]
    public async Task<IActionResult> CreatorChallenge(string code, CancellationToken ct)
    {
        NoCache();
        if (code.Length is < 6 or > 16) return NotFound();
        var dto = await challenge.GetAsync(0, code, ct);
        return dto is null ? NotFound(new { error = "not_found" }) : Ok(dto);
    }

    private void NoCache() => Response.Headers.CacheControl = "no-store";
}
