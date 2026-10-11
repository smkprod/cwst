using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Билдер колод: мета-колоды из боёв топа, которые игрок соберёт из своих карт, и
/// четыре колоды для КВ без общих карт. Только реально сыгранные колоды со статистикой.
/// </summary>
[ApiController]
[Route("api/builder")]
public class BuilderController(DeckBuilderUseCase builder) : ControllerBase
{
    /// <summary>
    /// GET /api/builder/decks?include=26000000,…&amp;exclude=…&amp;maxElixir=4&amp;owned=true&amp;sort=fit|win|popular&amp;lang=ru
    /// </summary>
    [HttpGet("decks")]
    public async Task<IActionResult> Decks([FromQuery] string? include, [FromQuery] string? exclude,
        [FromQuery] double? maxElixir, [FromQuery] bool owned = false, [FromQuery] string? sort = null,
        [FromQuery] string? lang = null, CancellationToken ct = default)
    {
        var r = await builder.DecksAsync(UserId, Filter(include, exclude, maxElixir, owned, sort), lang, ct);
        return r is null ? NotFound(new { error = "no_meta" }) : Ok(r);
    }

    /// <summary>GET /api/builder/war?exclude=…&amp;maxElixir=…&amp;owned=true&amp;lang=ru — четыре колоды для КВ.</summary>
    [HttpGet("war")]
    public async Task<IActionResult> War([FromQuery] string? exclude, [FromQuery] double? maxElixir,
        [FromQuery] bool owned = false, [FromQuery] string? lang = null, CancellationToken ct = default)
    {
        var r = await builder.WarAsync(UserId, Filter(null, exclude, maxElixir, owned, "fit"), lang, ct);
        return r is null ? NotFound(new { error = "no_meta" }) : Ok(r);
    }

    /// <summary>GET /api/builder/cards — все карты игры с уровнями игрока, для фильтра.</summary>
    [HttpGet("cards")]
    public async Task<IActionResult> Cards(CancellationToken ct) => Ok(await builder.CardsAsync(UserId, ct));

    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    private static DeckBuilderUseCase.Filter Filter(string? include, string? exclude, double? maxElixir, bool owned, string? sort) =>
        new(Ids(include), Ids(exclude),
            maxElixir is > 0 and < 10 ? maxElixir : null,
            owned,
            sort is "win" or "popular" ? sort : "fit");

    private static int[] Ids(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Distinct().Take(8).ToArray();
}
