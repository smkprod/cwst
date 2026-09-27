using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.Payments;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// «Clanify Плюс» для самого игрока: статус, покупка пропуска и «Стоп-тильт».
///
/// Плюс продаётся игроку, а не клану: клан для него не нужен, нужен только
/// привязанный тег - без него разбирать нечего.
/// </summary>
[ApiController]
[Route("api/plus")]
public class PlusController(
    ITelegramBotClient bot,
    PlusAccess access,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IServiceSettingRepository settings,
    ILogger<PlusController> logger) : ControllerBase
{
    public record InvoiceRequest(int Days);
    public record AlertsRequest(bool Enabled);

    /// <summary>GET /api/plus — есть ли Плюс, до когда, цены и состояние оповещений.</summary>
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var status = await access.GetAsync(userId, ct);
        var offer = await PlusSales.ReadAsync(settings, ct);
        var prefs = await alertPrefs.GetAsync(userId, ct);
        var linked = await players.GetByTelegramIdAsync(userId, ct) is not null;

        return Ok(new
        {
            paywall = status.Paywall,
            active = status.Active,
            unlocked = status.Unlocked,
            until = status.Until,
            source = status.Source,
            trialUsed = status.TrialUsed,
            trialDays = offer.TrialDays,
            trialMinBattles = PlusSales.TrialMinBattles,
            price7 = offer.Price7,
            price30 = offer.Price30,
            onSale = offer.OnSale,
            linked,
            // null - человек не выбирал: с Плюсом «Стоп-тильт» включён по умолчанию
            tiltAlerts = prefs?.TiltAlerts ?? status.Active,
            dmBlocked = prefs?.DmBlocked ?? false,
        });
    }

    /// <summary>
    /// POST /api/plus/invoice — счёт на пропуск (7 или 30 дней) для openInvoice.
    /// Цена и срок зашиваются в сам счёт: поменяй владелец цену, пока счёт открыт,
    /// человек всё равно заплатит ровно то, что было на кнопке.
    /// </summary>
    [HttpPost("invoice")]
    public async Task<IActionResult> Invoice([FromBody] InvoiceRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var offer = await PlusSales.ReadAsync(settings, ct);
        if (!offer.OnSale) return StatusCode(403, new { error = "not_on_sale" });

        var stars = offer.PriceFor(req.Days);
        if (stars <= 0) return BadRequest(new { error = "bad_days" });

        try
        {
            var link = await bot.CreateInvoiceLink(
                title: $"Clanify Плюс · {req.Days} дн.",
                description: "Полный разбор боёв, «Стоп-тильт» — напишу, когда пора сделать паузу, — " +
                             "и контры к твоим колодам по боям топа. Разовый пропуск, без автопродления.",
                payload: PlusSales.Payload(userId, req.Days, stars),
                currency: SponsorSales.Currency,
                prices: [new LabeledPrice($"Плюс на {req.Days} дн.", stars)],
                providerToken: "",
                cancellationToken: ct);
            return Ok(new { link });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create Plus invoice for {User}", userId);
            return StatusCode(502, new { error = "invoice_failed" });
        }
    }

    /// <summary>POST /api/plus/alerts — включить или выключить «Стоп-тильт».</summary>
    [HttpPost("alerts")]
    public async Task<IActionResult> Alerts([FromBody] AlertsRequest req, CancellationToken ct)
    {
        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        var prefs = await alertPrefs.GetOrCreateAsync(userId, ct);
        prefs.TiltAlerts = req.Enabled;
        // Включил заново - значит, разрешил писать (приложение спрашивает это право
        // перед включением). Прошлую неудачу доставки больше не считаем приговором.
        if (req.Enabled) prefs.DmBlocked = false;
        await alertPrefs.SaveChangesAsync(ct);
        return Ok(new { tiltAlerts = req.Enabled });
    }
}
