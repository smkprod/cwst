using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>Задания дня: что выпало сегодня, серия, отметки заданий из приложения.</summary>
[ApiController]
[Route("api/quests")]
public class QuestsController(DailyQuestsUseCase quests) : ControllerBase
{
    public record PingRequest(string Kind);

    public record RemindRequest(bool On);

    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    /// <summary>GET /api/quests — задания на сегодня по свежему журналу боёв, серия и клан.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dto = await quests.GetAsync(UserId, ct);
        return dto is null ? NotFound(new { error = "player_not_linked" }) : Ok(dto);
    }

    /// <summary>POST /api/quests/ping — задание в приложении сделано: trainer, tip или decks.</summary>
    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromBody] PingRequest req, CancellationToken ct)
    {
        var dto = await quests.PingAsync(UserId, req.Kind ?? "", ct);
        return dto is null ? NotFound(new { error = "player_not_linked" }) : Ok(dto);
    }

    /// <summary>POST /api/quests/remind — вечернее «серия сгорит»: включить или выключить.</summary>
    [HttpPost("remind")]
    public async Task<IActionResult> Remind([FromBody] RemindRequest req, CancellationToken ct)
    {
        await quests.SetRemindAsync(UserId, req.On, ct);
        return Ok(new { on = req.On });
    }
}
