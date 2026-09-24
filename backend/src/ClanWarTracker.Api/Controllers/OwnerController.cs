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
    public record AddModeratorRequest(string Username, string? Note, string[]? Permissions);
    public record SetPermissionsRequest(string[] Permissions);

    /// <summary>GET /api/owner/me — кто я для сервиса. Фронт по этому решает, показывать ли панель.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        return Ok(new
        {
            role = me.Role.ToString().ToLowerInvariant(),
            // Списком строк, а не числом: фронту нужно рисовать галочки, и число
            // пришлось бы разбирать у него второй, отдельной копией правил.
            permissions = PermissionNames(me.Permissions),
        });
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
        if (await DenyAsync(ServicePermission.Plans, ct) is { } deny) return deny;

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
        if (await DenyAsync(ServicePermission.Broadcast, ct) is { } deny) return deny;

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
        if (await DenyAsync(ServicePermission.DeleteClans, ct) is { } deny) return deny;

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
        if (await DenyAsync(ServicePermission.Maintenance, ct) is { } deny) return deny;

        var result = await harvestTop.ExecuteAsync(force: true, ct: ct);
        return Ok(new { rows = result.Rows, problem = result.Skipped });
    }

    /// <summary>GET /api/owner/moderators — список модераторов (видит только владелец).</summary>
    [HttpGet("moderators")]
    public async Task<IActionResult> GetModerators(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var list = await moderators.GetAllAsync(ct);
        return Ok(list.Select(m => new
        {
            m.Id,
            username = m.TelegramUsername,
            m.Note,
            permissions = PermissionNames(m.Permissions),
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
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var username = ServiceModerator.NormalizeUsername(req.Username ?? "");
        // Правила Telegram: 5–32 знака, латиница, цифры и подчёркивание.
        if (username.Length is < 5 or > 32 || !username.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            return BadRequest(new { error = "bad_username", message = "Юзернейм: 5–32 знака, латиница, цифры, _" });

        var existing = await moderators.GetAllAsync(ct);
        if (existing.Any(m => m.TelegramUsername == username))
            return Conflict(new { error = "already_moderator" });

        var granted = await GrantableAsync(req.Permissions, ct);
        if (granted is null) return StatusCode(403, new { error = "cannot_grant" });

        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        await moderators.AddAsync(new ServiceModerator
        {
            TelegramUsername = username,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            Permissions = granted.Value,
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
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var target = await moderators.GetByIdAsync(id, ct);
        if (target is null) return NotFound(new { error = "moderator_not_found" });

        await moderators.RemoveAsync(target, ct);
        await moderators.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    /// <summary>PUT /api/owner/moderators/{id}/permissions — поменять набор прав.</summary>
    [HttpPut("moderators/{id:int}/permissions")]
    public async Task<IActionResult> SetPermissions(
        int id, [FromBody] SetPermissionsRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var target = await moderators.GetByIdAsync(id, ct);
        if (target is null) return NotFound(new { error = "moderator_not_found" });

        var granted = await GrantableAsync(req.Permissions, ct);
        if (granted is null) return StatusCode(403, new { error = "cannot_grant" });

        target.Permissions = granted.Value;
        await moderators.SaveChangesAsync(ct);
        return Ok(new { ok = true, permissions = PermissionNames(target.Permissions) });
    }

    /// <summary>
    /// Запрошенный набор прав, если выдающий вправе его выдать. null — не вправе.
    ///
    /// Выдать больше, чем есть у тебя самого, нельзя. Без этого правила модератор,
    /// которому доверили назначать других, выписал бы себе рассылку и тарифы за два
    /// нажатия — право назначать превратилось бы в право на всё сразу.
    /// Владельца это не касается: у него есть всё, и проверка для него всегда верна.
    /// </summary>
    private async Task<ServicePermission?> GrantableAsync(string[]? names, CancellationToken ct)
    {
        var wanted = ParsePermissions(names);
        var me = await CurrentAsync(ct);
        return me.Can(wanted) ? wanted : null;
    }

    /// <summary>
    /// Имена прав в набор флагов.
    ///
    /// Неизвестное имя молча пропускаем, а не падаем: фронт может оказаться старее
    /// сервера после выката, и в этом случае лучше сохранить то, что он прислал,
    /// чем отказать целиком и оставить человека вообще без прав.
    /// </summary>
    private static ServicePermission ParsePermissions(string[]? names)
    {
        var result = ServicePermission.None;
        foreach (var name in names ?? [])
            if (Enum.TryParse<ServicePermission>(name, ignoreCase: true, out var one)
                && one != ServicePermission.All && one != ServicePermission.None)
                result |= one;
        return result;
    }

    /// <summary>Набор флагов обратно в имена — по одному на каждый выставленный.</summary>
    private static string[] PermissionNames(ServicePermission permissions) =>
        Enum.GetValues<ServicePermission>()
            .Where(p => p != ServicePermission.None && p != ServicePermission.All)
            .Where(p => (permissions & p) == p)
            .Select(p => p.ToString())
            .ToArray();

    private Task<ServiceIdentity> CurrentAsync(CancellationToken ct) =>
        access.ResolveAsync(
            (long)HttpContext.Items["TelegramUserId"]!,
            HttpContext.Items["TelegramUsername"] as string,
            ct);

    /// <summary>Отказ, если панель недоступна вовсе. null — можно.</summary>
    private async Task<IActionResult?> DenyViewAsync(CancellationToken ct) =>
        (await CurrentAsync(ct)).HasPanel ? null : StatusCode(403, new { error = "not_owner" });

    /// <summary>
    /// Отказ, если нет конкретного права. null — можно.
    ///
    /// Код ошибки отличается от «панели нет вообще»: человек в панель попал, и
    /// сказать ему нужно не «тебя тут нет», а «этого тебе не выдали».
    /// </summary>
    private async Task<IActionResult?> DenyAsync(ServicePermission permission, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        if (!me.HasPanel) return StatusCode(403, new { error = "not_owner" });
        return me.Can(permission)
            ? null
            : StatusCode(403, new { error = "no_permission", message = permission.ToString() });
    }
}
