using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>Опрос «что добавить»: отвечает сам игрок из приложения.</summary>
[ApiController]
[Route("api/feedback")]
public class FeedbackController(FeedbackUseCase feedback) : ControllerBase
{
    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    public record SubmitRequest(List<string>? Wants, string? Reason, string? Text);

    /// <summary>GET /api/feedback — отвечал ли и что спрашивать (покупателя и остальных - по-разному).</summary>
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await feedback.MineAsync(UserId, ct));

    /// <summary>POST /api/feedback — ответ. За первый - пара дней Плюса.</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitRequest req, CancellationToken ct)
    {
        var (outcome, granted) = await feedback.SubmitAsync(UserId, req.Wants, req.Reason, req.Text, ct);
        return outcome == FeedbackUseCase.SubmitOutcome.Invalid
            ? BadRequest(new { error = "empty" })
            : Ok(new { ok = true, grantedDays = granted });
    }
}
