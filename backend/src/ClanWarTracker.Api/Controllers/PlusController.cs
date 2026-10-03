using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.Payments;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// «Clanify Плюс» для игрока: статус, покупка себе или в подарок, «Стоп-тильт».
///
/// Спонсорство и Плюс - одна линейка: спонсор получает весь Плюс плюс статус и две
/// подарочные недели в месяц для соклановцев. Поэтому статус спонсора отдаётся
/// здесь же - окно покупки у них одно.
/// </summary>
[ApiController]
[Route("api/plus")]
public class PlusController(
    ITelegramBotClient bot,
    PlusAccess access,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IEntitlementRepository entitlements,
    IServiceSettingRepository settings,
    GetTiltProfileUseCase tiltProfile,
    INotificationSender sender,
    ILogger<PlusController> logger) : ControllerBase
{
    /// <param name="RecipientTag">Кому подарить. null - себе.</param>
    public record InvoiceRequest(int Days, string? RecipientTag);

    /// <summary>Настройки «Стоп-тильта»: null - поле не меняется. DailyLossLimit 0 - выключить лимит.</summary>
    public record AlertsRequest(bool? Enabled, int? LossThreshold, int? DailyLossLimit, bool? QuietHours);

    public record GiftRequest(string RecipientTag);

    private long UserId => (long)HttpContext.Items["TelegramUserId"]!;

    /// <summary>GET /api/plus — Плюс, спонсорство, цены, подарки и состояние оповещений.</summary>
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await access.GetAsync(UserId, ct);
        var offer = await PlusSales.ReadAsync(settings, ct);
        var sponsorOffer = await SponsorSales.ReadAsync(settings, ct);
        var prefs = await alertPrefs.GetAsync(UserId, ct);
        var me = await players.GetByTelegramIdAsync(UserId, ct);
        var now = DateTime.UtcNow;
        var isSponsor = me?.IsSponsor(now) == true;

        return Ok(new
        {
            paywall = status.Paywall,
            active = status.Active,
            unlocked = status.Unlocked,
            until = status.Until,
            source = status.Source,
            price7 = offer.Price7,
            price30 = offer.Price30,
            onSale = offer.OnSale,
            // Личная скидка - только на себя: подарок другому по обычной цене
            promo7 = PlusSales.Promo(prefs, now)?.Price7,
            promo30 = PlusSales.Promo(prefs, now)?.Price30,
            promoUntil = PlusSales.Promo(prefs, now)?.Until,
            linked = me is not null,
            myTag = me?.PlayerTag,
            tiltAlerts = status.Unlocked ? prefs?.TiltAlerts != false : prefs?.TiltAlerts == true,
            dmBlocked = prefs?.DmBlocked ?? false,
            freeSignalsLeft = prefs?.FreeSignalsLeft ?? PlayerAlertPrefs.DefaultFreeSignals,
            sponsorPrice = sponsorOffer.OnSale ? sponsorOffer.Stars : 0,
            sponsorDays = sponsorOffer.Days,
            isSponsor,
            sponsorUntil = isSponsor ? me!.SponsorUntilUtc : null,
            freeGiftsLeft = isSponsor ? await FreeGiftsLeftAsync(now, ct) : 0,
        });
    }

    /// <summary>GET /api/plus/tilt — экран «🧊 Стоп-тильт»: тип, налог, серии, сигналы.</summary>
    [HttpGet("tilt")]
    public async Task<IActionResult> Tilt(CancellationToken ct)
    {
        var dto = await tiltProfile.ExecuteAsync(UserId, ct);
        return dto is null ? NotFound(new { error = "player_not_linked" }) : Ok(dto);
    }

    /// <summary>
    /// POST /api/plus/invoice — счёт на пропуск (7 или 30 дней) для openInvoice. С тегом
    /// получателя - подарок: доступ достанется ему, а платит тот, кто откроет счёт.
    /// Тот же счёт на себя годится для «попросить в подарок»: его пересылают тому,
    /// кто заплатит, - плательщик неважен, получатель зашит в счёт.
    /// </summary>
    [HttpPost("invoice")]
    public async Task<IActionResult> Invoice([FromBody] InvoiceRequest req, CancellationToken ct)
    {
        var offer = await PlusSales.ReadAsync(settings, ct);
        if (!offer.OnSale) return StatusCode(403, new { error = "not_on_sale" });

        var stars = offer.PriceFor(req.Days);
        if (stars <= 0) return BadRequest(new { error = "bad_days" });
        // Себе - по личной скидке, если она есть. Сумма зашивается в счёт, оплата её сверит.
        if (string.IsNullOrWhiteSpace(req.RecipientTag)
            && PlusSales.Promo(await alertPrefs.GetAsync(UserId, ct), DateTime.UtcNow) is { } promo)
            stars = req.Days == 7 ? promo.Price7 : req.Days == 30 ? promo.Price30 : stars;

        var recipient = UserId;
        string? recipientName = null;
        if (!string.IsNullOrWhiteSpace(req.RecipientTag))
        {
            var target = await players.GetByTagAsync(LinkPlayerUseCase.Normalize(req.RecipientTag), ct);
            if (target is null) return NotFound(new { error = "player_not_found" });
            // Доступ - на аккаунт Telegram. Не привязан - выдать некому, и звёзды ушли бы в никуда.
            if (target.TelegramUserId is not long tg) return BadRequest(new { error = "recipient_not_linked" });
            recipient = tg;
            recipientName = target.Name;
        }

        try
        {
            var gift = recipient != UserId;
            var link = await bot.CreateInvoiceLink(
                title: gift ? $"Clanify Плюс в подарок · {req.Days} дн." : $"Clanify Плюс · {req.Days} дн.",
                description: (gift ? $"Для {recipientName}. " : "") +
                             "Стоп-тильт: бот напишет «стоп» прямо во время серии поражений — с паузой и итогом захода. " +
                             "И полный разбор боёв. Разовый пропуск, без автопродления.",
                payload: PlusSales.Payload(recipient, req.Days, stars),
                currency: SponsorSales.Currency,
                prices: [new LabeledPrice($"Плюс на {req.Days} дн.", stars)],
                providerToken: "",
                cancellationToken: ct);
            return Ok(new { link });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create Plus invoice for {User}", UserId);
            return StatusCode(502, new { error = "invoice_failed" });
        }
    }

    /// <summary>
    /// POST /api/plus/gift-free — спонсор дарит соклановцу неделю Плюса бесплатно,
    /// две в месяц. Так спонсорство покупает лидер: это и статус, и чем наградить своих.
    /// </summary>
    [HttpPost("gift-free")]
    public async Task<IActionResult> GiftFree([FromBody] GiftRequest req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var me = await players.GetByTelegramIdAsync(UserId, ct);
        if (me is null || !me.IsSponsor(now)) return StatusCode(403, new { error = "not_sponsor" });
        if (await FreeGiftsLeftAsync(now, ct) <= 0) return Conflict(new { error = "no_gifts_left" });

        var target = await players.GetByTagAsync(LinkPlayerUseCase.Normalize(req.RecipientTag ?? ""), ct);
        if (target is null) return NotFound(new { error = "player_not_found" });
        if (target.TelegramUserId is not long tg) return BadRequest(new { error = "recipient_not_linked" });
        if (tg == UserId) return BadRequest(new { error = "self_gift" });

        var until = await access.GrantAsync(tg, PlusSales.SponsorFreeGiftDays, Entitlement.Sources.Gift,
            target.PlayerTag, 0, null, ct, giverTelegramUserId: UserId);

        // Подарок, о котором не знают, не работает ни как подарок, ни как реклама.
        var prefs = await alertPrefs.GetAsync(tg, ct);
        var t = BotText.For(prefs?.Lang);
        await sender.TrySendToUserAsync(tg, string.Format(t.GiftReceived, me.Name, until.ToString("dd.MM.yyyy")), ct);

        return Ok(new { recipient = target.Name, until, freeGiftsLeft = await FreeGiftsLeftAsync(now, ct) });
    }

    /// <summary>POST /api/plus/alerts — включить «Стоп-тильт» и настроить правила.</summary>
    [HttpPost("alerts")]
    public async Task<IActionResult> Alerts([FromBody] AlertsRequest req, CancellationToken ct)
    {
        var prefs = await alertPrefs.GetOrCreateAsync(UserId, ct);
        if (req.Enabled is bool enabled)
        {
            prefs.TiltAlerts = enabled;
            // Включил заново - значит, разрешил писать (приложение спрашивает это право
            // перед включением). Прошлую неудачу доставки больше не считаем приговором.
            if (enabled) prefs.DmBlocked = false;
        }

        // Правила - часть Плюса: без него они бы ничего не меняли, а обещали бы.
        var status = await access.GetAsync(UserId, ct);
        if (status.Unlocked)
        {
            if (req.LossThreshold is int threshold) prefs.LossThreshold = Math.Clamp(threshold, 2, 3);
            if (req.DailyLossLimit is int limit) prefs.DailyLossLimit = limit <= 0 ? null : Math.Clamp(limit, 3, 20);
            if (req.QuietHours is bool quiet) prefs.QuietHours = quiet;
        }

        await alertPrefs.SaveChangesAsync(ct);
        return Ok(new
        {
            tiltAlerts = prefs.TiltAlerts,
            lossThreshold = prefs.LossThreshold,
            dailyLossLimit = prefs.DailyLossLimit,
            quietHours = prefs.QuietHours,
        });
    }

    private async Task<int> FreeGiftsLeftAsync(DateTime now, CancellationToken ct) =>
        Math.Max(0, PlusSales.SponsorFreeGiftsPerMonth
                    - await entitlements.CountFreeGiftsSinceAsync(UserId, now.AddDays(-30), ct));
}
