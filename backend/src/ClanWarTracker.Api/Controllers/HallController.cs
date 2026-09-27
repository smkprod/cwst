using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
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
public class HallController(
    GetHallOfFameUseCase hall,
    GetClanPageUseCase clanPage,
    GetPlayerPageUseCase playerPage,
    SetClanPageUseCase setClanPage,
    SendClanMessageUseCase sendMessage,
    IPlayerRepository players) : ControllerBase
{
    /// <param name="Kind">«challenge» — вызов на бой, иначе обычное сообщение.</param>
    public record MessageRequest(string Text, string? Kind);

    /// <param name="DesignKey">null — не трогать оформление.</param>
    /// <param name="Motto">null — не трогать девиз; пустая строка — убрать.</param>
    public record PageRequest(string? DesignKey, string? Motto);

    /// <summary>GET /api/hall — топ игроков и кланов за текущий сезон и своё место в нём.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var me = await players.GetByTelegramIdAsync(userId, ct);
        var dto = await hall.ExecuteAsync(me?.PlayerTag, ct);
        // Снимков ещё нет — это не ошибка, а «рано»: клиент покажет объяснение.
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>
    /// GET /api/hall/clans/{clanId} — страница клана.
    ///
    /// Открыта по любому клану сервиса, а не только своему: на Аллее по чужим и
    /// нажимают. Показывает ровно то, что и так видно в списке, плюс состав и
    /// историю недель — ничего, что клан скрывал бы от посторонних.
    /// </summary>
    [HttpGet("clans/{clanId:int}")]
    public async Task<IActionResult> ClanPage(int clanId, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var dto = await clanPage.ExecuteAsync(clanId, userId, ct);
        return dto is null ? NotFound(new { error = "clan_not_found" }) : Ok(dto);
    }

    /// <summary>
    /// GET /api/hall/players/{tag} — страница игрока.
    ///
    /// Тег в адресе идёт без решётки: клиент её срезает, а сервер принимает оба вида.
    /// </summary>
    [HttpGet("players/{tag}")]
    public async Task<IActionResult> PlayerPage(string tag, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var normalized = tag.StartsWith('#') ? tag : "#" + tag;
        var dto = await playerPage.ExecuteAsync(normalized, userId, ct);
        return dto is null ? NotFound(new { error = "player_not_found" }) : Ok(dto);
    }

    /// <summary>
    /// POST /api/hall/clans/{clanId}/page — оформление и девиз страницы своего клана.
    /// </summary>
    [HttpPost("clans/{clanId:int}/page")]
    public async Task<IActionResult> SavePage(
        int clanId, [FromBody] PageRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var error = await setClanPage.ExecuteAsync(userId, clanId, req.DesignKey, req.Motto, ct);
        if (error is null) return Ok(new { ok = true });

        return error.Value switch
        {
            ClanPageError.PlayerNotLinked => NotFound(new { error = "player_not_linked" }),
            ClanPageError.ClanNotFound => NotFound(new { error = "clan_not_found" }),
            ClanPageError.NotMyClan => StatusCode(403, new { error = "not_my_clan" }),
            ClanPageError.NotAllowed => StatusCode(403, new { error = "not_allowed" }),
            ClanPageError.DesignNeedsSponsor => StatusCode(403, new { error = "design_needs_sponsor" }),
            ClanPageError.UnknownDesign => BadRequest(new { error = "unknown_design" }),
            ClanPageError.MottoTooLong => BadRequest(new { error = "motto_too_long" }),
            _ => StatusCode(500, new { error = "unknown" }),
        };
    }

    /// <summary>
    /// POST /api/hall/clans/{clanId}/message — написать клану от имени своего.
    ///
    /// Лежит тут, а не в кланах, потому что точка входа — Аллея: именно там видно
    /// чужие кланы и именно оттуда по ним нажимают.
    /// </summary>
    [HttpPost("clans/{clanId:int}/message")]
    public async Task<IActionResult> SendMessage(
        int clanId, [FromBody] MessageRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var kind = string.Equals(req.Kind, "challenge", StringComparison.OrdinalIgnoreCase)
            ? ClanMessageKind.Challenge
            : ClanMessageKind.Message;

        var error = await sendMessage.ExecuteAsync(userId, clanId, req.Text ?? "", kind, ct);
        if (error is null) return Ok(new { ok = true });

        return error.Value switch
        {
            ClanMessageError.PlayerNotLinked => NotFound(new { error = "player_not_linked" }),
            ClanMessageError.NoClan or ClanMessageError.TargetNotFound
                => NotFound(new { error = "clan_not_found" }),
            ClanMessageError.SameClan => BadRequest(new { error = "same_clan" }),
            ClanMessageError.NotAllowed => StatusCode(403, new { error = "not_allowed" }),
            ClanMessageError.TargetOptedOut => StatusCode(403, new { error = "target_opted_out" }),
            ClanMessageError.TargetHasNoChat => BadRequest(new { error = "target_has_no_chat" }),
            ClanMessageError.TooSoon => StatusCode(429, new { error = "too_soon" }),
            ClanMessageError.DailyLimit => StatusCode(429, new { error = "daily_limit" }),
            ClanMessageError.EmptyText => BadRequest(new { error = "empty_text" }),
            ClanMessageError.TooLong => BadRequest(new { error = "too_long" }),
            ClanMessageError.ChallengeNeedsSponsor => StatusCode(403, new { error = "needs_sponsor" }),
            _ => StatusCode(500, new { error = "unknown" }),
        };
    }
}
