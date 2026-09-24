using ClanWarTracker.Application.Security;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Панель сервиса: кланы, сводка, тарифы, рассылка, модераторы.
///
/// Два уровня доступа. Владелец (Owner:TelegramUserId) может всё. Модератор видит
/// то же, что владелец, но ничего не меняет: рассылка уходит двум сотням человек и
/// не отзывается, выданный тариф стоит денег, удаление клана уносит историю войн.
/// Поэтому право смотреть и право менять разделены явно, а не «по совести».
/// </summary>
[ApiController]
[Route("api/owner")]
public class OwnerController(
    IClanRepository clans,
    GetOwnerDashboardUseCase dashboard,
    GetOwnerClanDetailUseCase clanDetail,
    SetClanPlanUseCase setPlan,
    OwnerBroadcastUseCase broadcast,
    HarvestTopPlayersUseCase harvestTop,
    IServiceModeratorRepository moderators,
    ServiceAccess access) : ControllerBase
{
    public record SetPlanRequest(string Tier, int? Days);
    public record BroadcastRequest(string Text, string Target);
    public record AddModeratorRequest(string Username, string? Note);

    /// <summary>GET /api/owner/me — кто я для сервиса. Фронт по этому решает, показывать ли панель.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var role = await CurrentRoleAsync(ct);
        return Ok(new { role = role.ToString().ToLowerInvariant() });
    }

    /// <summary>GET /api/owner/clans — кланы сервиса: тариф, активность, привязки.</summary>
    [HttpGet("clans")]
    public async Task<IActionResult> GetClans(CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;
        return Ok(await dashboard.GetClansAsync(ct));
    }

    /// <summary>
    /// GET /api/owner/clans/{id} — детали клана: привязанные игроки с @username и ролями
    /// (главы первыми — с ними имеет смысл говорить про Pro).
    /// </summary>
    [HttpGet("clans/{id:int}")]
    public async Task<IActionResult> GetClanDetail(int id, CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;

        var detail = await clanDetail.ExecuteAsync(id, ct);
        return detail is null ? NotFound(new { error = "clan_not_found" }) : Ok(detail);
    }

    /// <summary>POST /api/owner/clans/{id}/plan — выдать тариф. Body: { tier: "pro"|"free", days?: 30 }.</summary>
    [HttpPost("clans/{id:int}/plan")]
    public async Task<IActionResult> SetPlan(int id, [FromBody] SetPlanRequest req, CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var tier = req.Tier?.ToLowerInvariant() switch
        {
            "pro" => PlanTier.Pro,
            "free" => PlanTier.Free,
            _ => (PlanTier?)null,
        };
        if (tier is null) return BadRequest(new { error = "bad_tier", message = "tier: pro | free" });

        var ok = await setPlan.ExecuteAsync(id, tier.Value, req.Days, ct);
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "clan_not_found" });
    }

    /// <summary>GET /api/owner/stats — детальная сводка по сервису.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;
        return Ok(await dashboard.GetStatsAsync(ct));
    }

    /// <summary>
    /// POST /api/owner/broadcast — ручная рассылка. Body: { text, target: "dm"|"chats"|"both" }.
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req, CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var text = req.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return BadRequest(new { error = "empty_text" });
        if (text.Length > 4000) return BadRequest(new { error = "too_long", message = "Макс 4000 символов" });

        var target = req.Target?.ToLowerInvariant();
        var toDm = target is "dm" or "both";
        var toChats = target is "chats" or "both";
        if (!toDm && !toChats) return BadRequest(new { error = "bad_target", message = "target: dm | chats | both" });

        var r = await broadcast.ExecuteAsync(text, toDm, toChats, ct);
        return Ok(new { sentDm = r.SentDm, sentChats = r.SentChats, failedDm = r.FailedDm, failedChats = r.FailedChats });
    }

    /// <summary>
    /// DELETE /api/owner/clans/{id} — отвязать клан от сервиса.
    /// Удаляет клан вместе с привязками игроков и историей войн.
    /// </summary>
    [HttpDelete("clans/{id:int}")]
    public async Task<IActionResult> DeleteClan(int id, CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == id);
        if (clan is null) return NotFound(new { error = "clan_not_found" });

        await clans.RemoveAsync(clan, ct);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// POST /api/owner/top/harvest — собрать снимок мирового топа прямо сейчас.
    ///
    /// Сбор живёт в суточном цикле воркера, и до этой ручки единственным способом
    /// проверить, работает ли он, было ждать следующего тика и идти читать логи.
    /// Когда снимок не собирается, проверять приходится часто, а ждать — некогда.
    /// </summary>
    [HttpPost("top/harvest")]
    public async Task<IActionResult> HarvestTop(CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var result = await harvestTop.ExecuteAsync(force: true, ct: ct);
        return Ok(new { rows = result.Rows, problem = result.Skipped });
    }

    /// <summary>GET /api/owner/moderators — список модераторов (видит только владелец).</summary>
    [HttpGet("moderators")]
    public async Task<IActionResult> GetModerators(CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var list = await moderators.GetAllAsync(ct);
        return Ok(list.Select(m => new
        {
            m.Id,
            username = m.TelegramUsername,
            m.Note,
            m.AddedAtUtc,
            m.FirstSeenAtUtc,
            // Пока пусто — человек ещё ни разу не заходил, и запись держится
            // только на юзернейме. Фронт этим состоянием объясняет, почему так.
            confirmed = m.TelegramUserId is not null,
        }));
    }

    /// <summary>POST /api/owner/moderators — назначить по юзернейму. Body: { username, note? }.</summary>
    [HttpPost("moderators")]
    public async Task<IActionResult> AddModerator([FromBody] AddModeratorRequest req, CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var username = ServiceModerator.NormalizeUsername(req.Username ?? "");
        // Правила Telegram: 5–32 знака, латиница, цифры и подчёркивание.
        if (username.Length is < 5 or > 32 || !username.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            return BadRequest(new { error = "bad_username", message = "Юзернейм: 5–32 знака, латиница, цифры, _" });

        var existing = await moderators.GetAllAsync(ct);
        if (existing.Any(m => m.TelegramUsername == username))
            return Conflict(new { error = "already_moderator" });

        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        await moderators.AddAsync(new ServiceModerator
        {
            TelegramUsername = username,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            AddedAtUtc = DateTime.UtcNow,
            AddedByTelegramUserId = userId,
        }, ct);
        await moderators.SaveChangesAsync(ct);

        return Ok(new { ok = true, username });
    }

    /// <summary>DELETE /api/owner/moderators/{id} — снять модератора.</summary>
    [HttpDelete("moderators/{id:int}")]
    public async Task<IActionResult> RemoveModerator(int id, CancellationToken ct)
    {
        if (DenyNotOwner() is { } deny) return deny;

        var target = await moderators.GetByIdAsync(id, ct);
        if (target is null) return NotFound(new { error = "moderator_not_found" });

        await moderators.RemoveAsync(target, ct);
        await moderators.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    private async Task<ServiceRole> CurrentRoleAsync(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var username = HttpContext.Items["TelegramUsername"] as string;
        return await access.ResolveAsync(userId, username, ct);
    }

    /// <summary>Отказ, если смотреть нельзя вообще. null — можно.</summary>
    private async Task<IActionResult?> DenyViewAsync(CancellationToken ct) =>
        await CurrentRoleAsync(ct) == ServiceRole.None
            ? StatusCode(403, new { error = "not_owner" })
            : null;

    /// <summary>
    /// Отказ, если действие доступно только владельцу. null — можно.
    ///
    /// Владельца проверяем без похода в базу: он известен из конфига, и лишний
    /// запрос тут ничего не уточнит.
    /// </summary>
    private IActionResult? DenyNotOwner() =>
        access.IsOwner((long)HttpContext.Items["TelegramUserId"]!)
            ? null
            : StatusCode(403, new { error = "not_owner" });
}
