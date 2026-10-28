using ClanWarTracker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Оплата гривнами через банку Монобанка: заказ с кодом и проверка, пришла ли оплата.
/// Сам перевод идёт в приложении банка; выдаёт доступ воркер по выписке банки.
/// </summary>
[ApiController]
[Route("api/jar")]
public class JarController(JarPaymentUseCase jar) : ControllerBase
{
    /// <param name="Product">plus или pro.</param>
    /// <param name="Days">Для Плюса - 7 или 30; у Клан Про срок свой.</param>
    public record OrderRequest(string Product, int Days);

    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    /// <summary>POST /api/jar/order — сумма, код для комментария и ссылка на банку.</summary>
    [HttpPost("order")]
    public async Task<IActionResult> Order([FromBody] OrderRequest req, CancellationToken ct)
    {
        var (order, error) = await jar.CreateOrderAsync(UserId, req.Product ?? "", req.Days, ct);
        return error switch
        {
            null => Ok(order),
            "jar_off" => StatusCode(403, new { error }),
            "player_not_linked" => NotFound(new { error }),
            _ => BadRequest(new { error }),
        };
    }

    /// <summary>GET /api/jar/order/{code} — оплачен ли заказ.</summary>
    [HttpGet("order/{code}")]
    public async Task<IActionResult> Get(string code, CancellationToken ct) =>
        await jar.GetOrderAsync(UserId, code, ct) is { } order ? Ok(order) : NotFound(new { error = "order_not_found" });
}
