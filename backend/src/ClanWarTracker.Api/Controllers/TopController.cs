using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Мировой топ: мета, список и карточка игрока.
///
/// Всё считается по нашим суточным снимкам, кроме карточки игрока — она живая,
/// потому что последние бои имеет смысл смотреть только свежими.
/// </summary>
[ApiController]
[Route("api/top")]
public class TopController(
    GetTopMetaUseCase meta,
    GetTopPlayersUseCase players,
    GetTopPlayerDetailUseCase detail) : ControllerBase
{
    /// <summary>GET /api/top/meta — популярность карт, динамика за неделю, порог входа.</summary>
    [HttpGet("meta")]
    public async Task<IActionResult> Meta(CancellationToken ct)
    {
        var dto = await meta.ExecuteAsync(ct);
        // Снимков ещё нет — это не ошибка, а состояние «копим». Клиент покажет
        // объяснение вместо пустого экрана.
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>GET /api/top/players?skip=0&amp;take=50 — страница списка топа.</summary>
    [HttpGet("players")]
    public async Task<IActionResult> Players([FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await players.ExecuteAsync(skip, take, ct));

    /// <summary>GET /api/top/players/{tag} — профиль, колода и последние бои.</summary>
    [HttpGet("players/{tag}")]
    public async Task<IActionResult> Detail(string tag, CancellationToken ct)
    {
        var dto = await detail.ExecuteAsync(LinkPlayerUseCase.Normalize(tag), ct);
        return dto is null ? NotFound(new { error = "player_not_found" }) : Ok(dto);
    }
}
