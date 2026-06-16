using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

[ApiController]
[Route("api/recruitment")]
public class RecruitmentController(
    IPlayerRepository players,
    IClanRepository clans,
    IWarSnapshotRepository snapshots,
    IRecruitmentRepository recruitment,
    IClashRoyaleApi crApi) : ControllerBase
{
    public record ApplyRequest(string? Note);

    /// <summary>GET /api/recruitment/me — текущий статус заявки игрока.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyStatus(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null) return NotFound(new { error = "player_not_linked" });

        var profile = await recruitment.GetByPlayerTagAsync(player.PlayerTag, ct);
        return Ok(new
        {
            isActive = profile?.IsActive ?? false,
            note = profile?.Note,
        });
    }

    /// <summary>POST /api/recruitment — подать/обновить заявку "ищу клан".</summary>
    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] ApplyRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null) return NotFound(new { error = "player_not_linked" });

        var note = req.Note?.Trim();
        if (note?.Length > 500) note = note[..500];

        var now = DateTime.UtcNow;
        await recruitment.UpsertAsync(new RecruitmentProfile
        {
            PlayerTag = player.PlayerTag,
            TelegramUserId = userId,
            Name = player.Name,
            Note = string.IsNullOrEmpty(note) ? null : note,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }, ct);
        await recruitment.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    /// <summary>DELETE /api/recruitment — отозвать заявку.</summary>
    [HttpDelete]
    public async Task<IActionResult> Withdraw(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null) return NotFound(new { error = "player_not_linked" });

        var profile = await recruitment.GetByPlayerTagAsync(player.PlayerTag, ct);
        if (profile is not null)
        {
            profile.IsActive = false;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            await recruitment.SaveChangesAsync(ct);
        }
        return Ok(new { ok = true });
    }

    /// <summary>
    /// GET /api/recruitment/candidates — список кандидатов с реальной КВ-статистикой.
    /// Только для лидера/сорука клана с Pro-тарифом.
    /// </summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var player = await players.GetByTelegramIdAsync(userId, ct);
        if (player is null) return NotFound(new { error = "player_not_linked" });

        var clan = await clans.GetByIdAsync(player.ClanId, ct);
        if (clan is null) return NotFound(new { error = "clan_not_found" });

        // Проверяем что тариф Pro
        if (clan.EffectivePlan(DateTime.UtcNow) != PlanTier.Pro)
            return StatusCode(403, new { error = "pro_required", message = "Биржа доступна только на тарифе Pro" });

        // Проверяем что лидер или сорук
        var crRole = await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player.PlayerTag, ct);
        if (crRole is not ("leader" or "coLeader"))
            return StatusCode(403, new { error = "not_leader", message = "Только лидер или соруководитель может просматривать кандидатов" });

        var profiles = await recruitment.GetActiveAsync(ct);
        if (profiles.Count == 0)
            return Ok(new { candidates = Array.Empty<object>() });

        // Обогащаем статистикой из снапшотов (до 8 последних недель)
        var tags = profiles.Select(p => p.PlayerTag).ToList();
        var history = await snapshots.GetPlayersHistoryAsync(tags, 8, ct);

        var statsByTag = history
            .GroupBy(h => h.PlayerTag)
            .ToDictionary(g => g.Key, g => new
            {
                TotalFame = g.Sum(h => h.Fame),
                WeeksPlayed = g.Count(),
                AvgFame = g.Any(h => h.DecksUsed > 0)
                    ? Math.Round(g.Where(h => h.DecksUsed > 0).Average(h => (double)h.Fame / h.DecksUsed), 1)
                    : 0.0,
            }, StringComparer.OrdinalIgnoreCase);

        var candidates = profiles.Select(p =>
        {
            statsByTag.TryGetValue(p.PlayerTag, out var s);
            return new
            {
                playerTag = p.PlayerTag,
                name = p.Name,
                note = p.Note,
                telegramUserId = p.TelegramUserId,
                totalFame = s?.TotalFame ?? 0,
                weeksPlayed = s?.WeeksPlayed ?? 0,
                avgFamePerAttack = s?.AvgFame ?? 0.0,
                updatedAtUtc = p.UpdatedAtUtc,
            };
        }).ToList();

        return Ok(new { candidates });
    }
}
