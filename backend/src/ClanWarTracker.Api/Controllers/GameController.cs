using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>Мини-игра «Карта дня»: одна загадка в сутки, общая для всех.</summary>
[ApiController]
[Route("api/game")]
public class GameController(DailyPuzzleUseCase puzzle, IPlayerRepository players) : ControllerBase
{
    /// <summary>GET /api/game/daily — состояние сегодняшней загадки.</summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily(CancellationToken ct)
    {
        var player = await CurrentPlayerAsync(ct);
        if (player is null)
            return NotFound(new { error = "player_not_linked", message = "Сначала привяжи тег: /link #ТЕГ" });

        var state = await Safe(() => puzzle.GetAsync(player, ct));
        return state is null
            ? StatusCode(503, new { error = "cr_api_unavailable", message = "Справочник карт недоступен" })
            : Ok(state);
    }

    /// <summary>POST /api/game/daily/guess — ответ игрока.</summary>
    [HttpPost("daily/guess")]
    public async Task<IActionResult> Guess([FromBody] GuessRequest body, CancellationToken ct)
    {
        var player = await CurrentPlayerAsync(ct);
        if (player is null)
            return NotFound(new { error = "player_not_linked", message = "Сначала привяжи тег: /link #ТЕГ" });

        var state = await Safe(() => puzzle.GuessAsync(player, body.CardId, ct));
        return state is null
            ? StatusCode(503, new { error = "cr_api_unavailable", message = "Справочник карт недоступен" })
            : Ok(state);
    }

    public record GuessRequest(int CardId);

    private async Task<Domain.Entities.Player?> CurrentPlayerAsync(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return await players.GetByTelegramIdAsync(userId, ct);
    }

    /// <summary>
    /// Недоступный справочник карт — это не повод показывать игроку ошибку игры и уж
    /// тем более не повод засчитывать промах. Возвращаем null, клиент прячет карточку.
    /// </summary>
    private static async Task<T?> Safe<T>(Func<Task<T?>> get) where T : class
    {
        try { return await get(); }
        catch { return null; }
    }
}
