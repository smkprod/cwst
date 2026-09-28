using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Оплата «Clanify Плюс» звёздами: проверка перед списанием и выдача после.
///
/// Устроена как оплата спонсорства и живёт рядом с ней: тот же обработчик в боте,
/// тот же журнал платежей. Отличается только тем, что выдаёт: срок доступа на
/// аккаунт Telegram, а не звезду на строку игрока.
/// </summary>
public class ProcessPlusPaymentUseCase(
    PlusAccess access,
    IEntitlementRepository entitlements,
    ISponsorPaymentRepository payments,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs)
{
    /// <summary>Можно ли принимать платёж. null - можно, иначе причина для плательщика.</summary>
    public Task<string?> ValidateAsync(string currency, int totalAmount, string payload)
    {
        if (currency != SponsorSales.Currency) return Task.FromResult<string?>("Оплата принимается только звёздами.");
        if (PlusSales.Parse(payload) is not { } order)
            return Task.FromResult<string?>("Счёт повреждён — открой оплату заново.");
        // Сумма зашита в счёт при создании: расхождение значит, что счёт собран не нами.
        if (order.Stars != totalAmount)
            return Task.FromResult<string?>("Сумма не совпадает со счётом — открой оплату заново.");
        return Task.FromResult<string?>(null);
    }

    /// <param name="Duplicate">Этот платёж уже учтён - повторная доставка.</param>
    /// <param name="Gift">Платил не получатель: подарок или оплаченная просьба «подари мне».</param>
    public record Applied(
        long RecipientTelegramUserId, DateTime Until, int Days, bool Duplicate,
        bool Gift = false, string? RecipientName = null, string? RecipientLang = null);

    /// <summary>
    /// Выдаёт Плюс за прошедший платёж. null - счёт не разобран, хотя деньги списаны:
    /// вызывающий обязан сообщить об этом громко и с номером платежа.
    /// </summary>
    public async Task<Applied?> ApplyAsync(
        long payerTelegramUserId, string payload, int totalAmount, string chargeId, CancellationToken ct = default)
    {
        if (PlusSales.Parse(payload) is not { } order) return null;

        if (await payments.ExistsAsync(chargeId, ct))
        {
            var until = await entitlements.UntilAsync(order.RecipientTelegramUserId, Entitlement.Skus.Plus, ct);
            return new Applied(order.RecipientTelegramUserId, until ?? DateTime.UtcNow, order.Days, Duplicate: true);
        }

        var player = await players.GetByTelegramIdAsync(order.RecipientTelegramUserId, ct);
        var stars = totalAmount > 0 ? totalAmount : order.Stars;
        var gift = payerTelegramUserId != order.RecipientTelegramUserId;

        var newUntil = await access.GrantAsync(
            order.RecipientTelegramUserId, order.Days,
            gift ? Entitlement.Sources.Gift : Entitlement.Sources.Purchase,
            player?.PlayerTag, stars, chargeId, ct, save: false,
            giverTelegramUserId: gift ? payerTelegramUserId : null);

        // Заплатил - значит, в личке с ботом он есть: прошлую неудачу доставки забываем,
        // иначе купивший не получил бы ни одного сигнала, ради которых платил.
        var prefs = await alertPrefs.GetAsync(order.RecipientTelegramUserId, ct);
        if (!gift && prefs is { DmBlocked: true }) prefs.DmBlocked = false;

        await payments.AddAsync(new SponsorPayment
        {
            PayerTelegramUserId = payerTelegramUserId,
            PlayerTag = player?.PlayerTag ?? "",
            Stars = stars,
            Days = order.Days,
            TelegramChargeId = chargeId,
            PaidAtUtc = DateTime.UtcNow,
            Kind = SponsorPayment.Kinds.Plus,
        }, ct);

        // Платёж и доступ - одной транзакцией (один контекст на оба репозитория).
        // Повтор того же платежа упрётся в уникальный номер и откатит и доступ.
        await entitlements.SaveChangesAsync(ct);
        return new Applied(order.RecipientTelegramUserId, newUntil, order.Days, Duplicate: false,
            Gift: gift, RecipientName: player?.Name, RecipientLang: prefs?.Lang);
    }
}

/// <summary>
/// Отметка о возврате звёзд: платёж помечается возвращённым, купленное им отзывается.
///
/// Сам возврат (вызов Telegram) делает контроллер - слой приложения о Telegram не
/// знает. Сюда приходят уже после того, как Telegram звёзды вернул: отметить
/// возврат, которого не было, значило бы отобрать оплаченное.
/// </summary>
public class RevokePurchaseUseCase(
    ISponsorPaymentRepository payments,
    IEntitlementRepository entitlements,
    IPlayerRepository players,
    IServiceSettingRepository settings)
{
    public async Task<bool> MarkRefundedAsync(string chargeId, CancellationToken ct = default)
    {
        var payment = await payments.GetByChargeAsync(chargeId, ct);
        if (payment is null) return false;

        var now = DateTime.UtcNow;
        payment.RefundedAtUtc = now;

        // Всё, что выдано этим платежом: Плюс за Плюс, Плюс внутри спонсорства.
        foreach (var e in await entitlements.GetByChargeAsync(chargeId, ct))
            e.RevokedAtUtc ??= now;

        if (payment.Kind != SponsorPayment.Kinds.Plus)
        {
            // Спонсорство: срок укорачивается на оплаченные дни. Ниже «сейчас» не
            // уходит - закончившееся спонсорство просто снимается.
            var player = string.IsNullOrEmpty(payment.PlayerTag) ? null : await players.GetByTagAsync(payment.PlayerTag, ct);
            if (player?.SponsorUntilUtc is { } until)
            {
                var cut = until.AddDays(-payment.Days);
                player.SponsorUntilUtc = cut > now ? cut : null;
            }
        }

        await payments.SaveChangesAsync(ct);
        await HallCache.BumpAsync(settings, ct);
        return true;
    }
}
