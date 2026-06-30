using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Игровые турниры Clash Royale (отслеживание по тегу, живые данные из CR API).
/// Отдельно от клановых турниров-сеток.
/// </summary>
[ApiController]
[Route("api/game-tournaments")]
public class GameTournamentsController(
    GameTournamentService service,
    IPlayerRepository players,
    IConfiguration config) : ControllerBase
{
    public record AddRequest(string Tag, string? Password);

    /// <summary>GET /api/game-tournaments — список отслеживаемых турниров с живыми данными.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return Ok(await service.ListAsync(userId, ct));
    }

    /// <summary>GET /api/game-tournaments/{id} — один турнир с живой таблицей.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var dto = await service.GetAsync(id, userId, ct);
        return dto is null ? NotFound(new { error = "not_found" }) : Ok(dto);
    }

    /// <summary>POST /api/game-tournaments — добавить турнир. Body: { tag, password? }.</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        if (string.IsNullOrWhiteSpace(req.Tag))
            return BadRequest(new { error = "empty_tag", message = "Введи тег турнира" });

        var player = await players.GetByTelegramIdAsync(userId, ct);
        var name = player?.Name ?? "Организатор";

        GameTournamentService.AddResult result;
        try { result = await service.AddAsync(req.Tag, req.Password, userId, name, ct); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("CR API"))
            { return StatusCode(503, new { error = "cr_api_token_invalid", message = ex.Message }); }
        catch (HttpRequestException ex) when ((int)(ex.StatusCode ?? 0) is >= 500 or 429)
            { return StatusCode(503, new { error = "cr_api_unavailable", message = ex.Message }); }

        return result.Error switch
        {
            GameTournamentService.AddError.None => Ok(result.Tournament),
            GameTournamentService.AddError.NotFound =>
                NotFound(new { error = "tournament_not_found", message = "Турнир с таким тегом не найден в Clash Royale" }),
            GameTournamentService.AddError.AlreadyTracked =>
                Conflict(new { error = "already_tracked", message = "Этот турнир уже отслеживается" }),
            _ => BadRequest(new { error = "bad_tag", message = "Проверь тег турнира" }),
        };
    }

    /// <summary>DELETE /api/game-tournaments/{id} — удалить (организатор или владелец сервиса).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var isOwner = long.TryParse(config["Owner:TelegramUserId"], out var ownerId) && ownerId != 0 && userId == ownerId;
        var ok = await service.RemoveAsync(id, userId, isOwner, ct);
        return ok ? Ok(new { ok = true }) : StatusCode(403, new { error = "not_allowed" });
    }
}
