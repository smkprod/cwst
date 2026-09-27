using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.Payments;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Покупка спонсорства звёздами Telegram.
///
/// Раньше купить можно было только одним способом: написать владельцу в личку и
/// ждать, пока он выдаст руками. Каждая продажа зависела от того, проснулся ли
/// он, а Telegram к тому же требует продавать цифровое в ботах за звёзды.
/// </summary>
[ApiController]
[Route("api/sponsor")]
public class SponsorController(
    ITelegramBotClient bot,
    IPlayerRepository players,
    IServiceSettingRepository settings,
    ILogger<SponsorController> logger) : ControllerBase
{
    /// <summary>
    /// POST /api/sponsor/invoice — ссылка на счёт, которую приложение открывает
    /// через Telegram.WebApp.openInvoice.
    ///
    /// Счёт выставляется на того, кто нажал: в него зашиты его тег, срок и цена.
    /// Сама выдача происходит не здесь, а в боте, когда придёт подтверждение
    /// оплаты, — до этого момента деньги не списаны и выдавать нечего.
    /// </summary>
    [HttpPost("invoice")]
    public async Task<IActionResult> Invoice(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var me = await players.GetByTelegramIdAsync(userId, ct);
        if (me is null) return NotFound(new { error = "player_not_linked" });

        var offer = await SponsorSales.ReadAsync(settings, ct);
        if (!offer.OnSale) return StatusCode(403, new { error = "not_on_sale" });

        try
        {
            var link = await bot.CreateInvoiceLink(
                title: "★ Спонсорство Clanify",
                description: $"{offer.Days} дней: звезда у имени, свои фоны, фон и оформление клана, достижения картинкой.",
                // Строка, а не тег: тег не уникален, и по нему выдача ушла бы в чужую
                // старую строку того же игрока из другого клана.
                payload: SponsorSales.Payload(me.Id, offer.Days, offer.Stars),
                currency: SponsorSales.Currency,
                // Для звёзд Telegram требует ровно одну позицию и пустой токен провайдера
                prices: [new LabeledPrice($"Спонсорство на {offer.Days} дн.", offer.Stars)],
                providerToken: "",
                cancellationToken: ct);
            return Ok(new { link });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create sponsor invoice for {Tag}", me.PlayerTag);
            return StatusCode(502, new { error = "invoice_failed" });
        }
    }
}
