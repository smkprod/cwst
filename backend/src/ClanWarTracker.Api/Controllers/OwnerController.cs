using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Панель владельца сервиса: список кланов и выдача тарифов.
/// Доступ только для Telegram-аккаунта из конфига Owner:TelegramUserId.
/// </summary>
[ApiController]
[Route("api/owner")]
public class OwnerController(
    IClanRepository clans,
    IPlayerRepository players,
    SetClanPlanUseCase setPlan,
    OwnerBroadcastUseCase broadcast,
    IConfiguration config) : ControllerBase
{
    public record SetPlanRequest(string Tier, int? Days);
    public record BroadcastRequest(string Text, string Target);

    /// <summary>GET /api/owner/clans — все кланы сервиса с тарифами.</summary>
    [HttpGet("clans")]
    public async Task<IActionResult> GetClans(CancellationToken ct)
    {
        if (!IsOwner()) return StatusCode(403, new { error = "not_owner" });

        var now = DateTime.UtcNow;
        var list = new List<object>();
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            var clanPlayers = await players.GetByClanIdAsync(clan.Id, ct);
            list.Add(new
            {
                id = clan.Id,
                clanTag = clan.ClanTag,
                name = clan.Name,
                plan = clan.EffectivePlan(now) == PlanTier.Pro ? "pro" : "free",
                planExpiresAtUtc = clan.PlanExpiresAtUtc,
                linkedPlayers = clanPlayers.Count(p => p.TelegramUserId is not null),
            });
        }
        return Ok(list);
    }

    /// <summary>POST /api/owner/clans/{id}/plan — выдать тариф. Body: { tier: "pro"|"free", days?: 30 }.</summary>
    [HttpPost("clans/{id:int}/plan")]
    public async Task<IActionResult> SetPlan(int id, [FromBody] SetPlanRequest req, CancellationToken ct)
    {
        if (!IsOwner()) return StatusCode(403, new { error = "not_owner" });

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

    /// <summary>
    /// GET /api/owner/stats — воронка: сколько пользователей дошли до каждого шага.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (!IsOwner()) return StatusCode(403, new { error = "not_owner" });

        var allPlayers = await players.GetAllLinkedAsync(ct);
        var allClans  = await clans.GetAllAsync(ct);
        var now = DateTime.UtcNow;

        return Ok(new {
            totalClans       = allClans.Count,
            proClans         = allClans.Count(c => c.EffectivePlan(now) == Domain.Enums.PlanTier.Pro),
            totalLinkedUsers = allPlayers.Count,
            usersWithClan    = allPlayers.Count(p => p.ClanId.HasValue),
            usersWithoutClan = allPlayers.Count(p => !p.ClanId.HasValue),
            chatsWithBot     = allClans.Count(c => c.TelegramChatId != 0),
        });
    }

    /// <summary>
    /// POST /api/owner/broadcast — ручная рассылка. Body: { text, target: "dm"|"chats"|"both" }.
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req, CancellationToken ct)
    {
        if (!IsOwner()) return StatusCode(403, new { error = "not_owner" });

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
        if (!IsOwner()) return StatusCode(403, new { error = "not_owner" });

        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == id);
        if (clan is null) return NotFound(new { error = "clan_not_found" });

        await clans.RemoveAsync(clan, ct);
        return Ok(new { ok = true });
    }

    private bool IsOwner()
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        return long.TryParse(config["Owner:TelegramUserId"], out var ownerId)
               && ownerId != 0
               && userId == ownerId;
    }
}
