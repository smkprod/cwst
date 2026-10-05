using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>Лига дуэлей 1х1: профиль, топ, история и ссылка в друзья.</summary>
[ApiController]
[Route("api/duels")]
public class DuelController(DuelUseCase duels) : ControllerBase
{
    /// <summary>GET /api/duels — своя карточка, идущая дуэль, топ и последние дуэли.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return Ok(await duels.GetViewAsync(userId, ct));
    }

    public record LinkRequest(string? Link, string? Lang);

    /// <summary>POST /api/duels/link — вступить в лигу или сменить ссылку «добавить в друзья».</summary>
    [HttpPost("link")]
    public async Task<IActionResult> Link([FromBody] LinkRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var r = await duels.SetFriendLinkAsync(userId, req.Lang, req.Link ?? "", ct);
        return r.Outcome switch
        {
            DuelUseCase.LinkOutcome.NotLinked => NotFound(new { error = "player_not_linked" }),
            DuelUseCase.LinkOutcome.BadLink => BadRequest(new { error = "bad_link" }),
            DuelUseCase.LinkOutcome.WrongTag => BadRequest(new { error = "wrong_tag", linkTag = r.LinkTag, playerTag = r.PlayerTag }),
            _ => Ok(await duels.GetViewAsync(userId, ct)),
        };
    }
}
