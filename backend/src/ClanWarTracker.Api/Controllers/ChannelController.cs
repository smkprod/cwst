using ClanWarTracker.Application.Security;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Канал бота из панели: подключение, автопосты, ленты новостей, черновики и ручные
/// посты. Право то же, что у рассылки: пост в канал так же публичен и не отзывается.
/// </summary>
[ApiController]
[Route("api/owner/channel")]
public class ChannelController(ChannelUseCase channel, ServiceAccess access) : ControllerBase
{
    public record ConnectRequest(string? Handle);
    public record SettingsRequest(Dictionary<string, bool>? Toggles, List<string>? Feeds);
    public record ComposeRequest(string? Text, string? PhotoUrl, string? ButtonText, string? ButtonUrl);
    public record PublishRequest(string? Text, string? PhotoUrl);
    public record PreviewRequest(string? Kind);

    /// <summary>GET /api/owner/channel — канал, рубильники, ленты, черновики и последние посты.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        return Ok(await channel.GetAsync(ct));
    }

    /// <summary>POST /api/owner/channel/connect — привязать канал по @имени или ссылке.</summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        if (string.IsNullOrWhiteSpace(req.Handle)) return BadRequest(new { error = "empty" });
        return await channel.ConnectAsync(req.Handle, ct) switch
        {
            ChannelUseCase.ConnectResult.NotFound => NotFound(new { error = "not_found" }),
            ChannelUseCase.ConnectResult.NotAdmin => StatusCode(409, new { error = "not_admin" }),
            _ => Ok(await channel.GetAsync(ct)),
        };
    }

    /// <summary>POST /api/owner/channel/disconnect — отвязать канал. Посты в нём остаются.</summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        await channel.DisconnectAsync(ct);
        return Ok(await channel.GetAsync(ct));
    }

    /// <summary>POST /api/owner/channel/settings — рубильники автопостов и список лент.</summary>
    [HttpPost("settings")]
    public async Task<IActionResult> Settings([FromBody] SettingsRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        await channel.SaveSettingsAsync(req.Toggles, req.Feeds, ct);
        return Ok(await channel.GetAsync(ct));
    }

    /// <summary>POST /api/owner/channel/post — свой пост сразу в канал.</summary>
    [HttpPost("post")]
    public async Task<IActionResult> Compose([FromBody] ComposeRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        if (req.Text is { Length: > 4000 }) return BadRequest(new { error = "too_long" });
        return await Done(channel.ComposeAsync(req.Text, req.PhotoUrl, req.ButtonText, req.ButtonUrl, ct), ct);
    }

    /// <summary>POST /api/owner/channel/drafts/{id}/publish — опубликовать черновик, можно с правками.</summary>
    [HttpPost("drafts/{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, [FromBody] PublishRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        if (req.Text is { Length: > 4000 }) return BadRequest(new { error = "too_long" });
        return await Done(channel.PublishDraftAsync(id, req.Text, req.PhotoUrl, ct), ct);
    }

    /// <summary>POST /api/owner/channel/drafts/{id}/reject — убрать черновик.</summary>
    [HttpPost("drafts/{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        if (!await channel.RejectAsync(id, ct)) return NotFound(new { error = "not_found" });
        return Ok(await channel.GetAsync(ct));
    }

    /// <summary>POST /api/owner/channel/preview — собрать автопост черновиком прямо сейчас.</summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        if (!await channel.PreviewAsync(req.Kind ?? "", ct)) return StatusCode(409, new { error = "no_data" });
        return Ok(await channel.GetAsync(ct));
    }

    /// <summary>POST /api/owner/channel/news/refresh — проверить ленты сейчас, не дожидаясь таймера.</summary>
    [HttpPost("news/refresh")]
    public async Task<IActionResult> RefreshNews(CancellationToken ct)
    {
        if (await DenyAsync(ct) is { } deny) return deny;
        var r = await channel.FetchNewsAsync(null, ct);
        var state = await channel.GetAsync(ct);
        return Ok(new { published = r.Published, drafts = r.Drafts, state });
    }

    private async Task<IActionResult> Done(Task<ChannelUseCase.PostResult> task, CancellationToken ct) =>
        await task switch
        {
            ChannelUseCase.PostResult.NoChannel => StatusCode(409, new { error = "no_channel" }),
            ChannelUseCase.PostResult.Empty => BadRequest(new { error = "empty" }),
            ChannelUseCase.PostResult.NotFound => NotFound(new { error = "not_found" }),
            ChannelUseCase.PostResult.Failed => StatusCode(502, new { error = "telegram_failed" }),
            _ => Ok(await channel.GetAsync(ct)),
        };

    private async Task<IActionResult?> DenyAsync(CancellationToken ct)
    {
        var me = await access.ResolveAsync((long)HttpContext.Items["TelegramUserId"]!,
            HttpContext.Items["TelegramUsername"] as string, ct);
        if (!me.HasPanel) return StatusCode(403, new { error = "not_owner" });
        return me.Can(ServicePermission.Broadcast) ? null : StatusCode(403, new { error = "no_permission" });
    }
}
