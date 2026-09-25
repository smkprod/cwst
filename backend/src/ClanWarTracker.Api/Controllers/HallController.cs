using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Аллея славы: лучшие игроки и кланы сервиса за сезон.
///
/// Открыта всем привязанным, а не только своему клану: смысл витрины в том, что
/// на неё смотрят посторонние. Закрытая аллея — это просто ещё одна таблица.
/// </summary>
[ApiController]
[Route("api/hall")]
public class HallController(GetHallOfFameUseCase hall) : ControllerBase
{
    /// <summary>GET /api/hall — топ игроков и кланов за текущий сезон.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dto = await hall.ExecuteAsync(ct);
        // Снимков ещё нет — это не ошибка, а «рано»: клиент покажет объяснение.
        return dto is null ? NoContent() : Ok(dto);
    }
}
