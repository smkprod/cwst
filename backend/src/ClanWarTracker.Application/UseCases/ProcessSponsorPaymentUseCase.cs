using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Оплата спонсорства звёздами: проверка перед списанием и выдача после.
///
/// Два шага, а не один, потому что так устроен Telegram. Сначала бот получает
/// запрос «можно списывать?» и обязан ответить за десять секунд — здесь отсекается
/// всё, что выдать нельзя, пока деньги ещё не ушли. Потом приходит сообщение
/// «списано» — и тут отказывать уже поздно, остаётся только выдать.
/// </summary>
public class ProcessSponsorPaymentUseCase(
    IPlayerRepository players,
    ISponsorPaymentRepository payments,
    IServiceSettingRepository settings,
    PlusAccess plus)
{
    /// <summary>
    /// Можно ли принимать этот платёж. null — можно, иначе причина: Telegram
    /// покажет её плательщику вместо списания.
    /// </summary>
    public async Task<string?> ValidateAsync(
        string currency, int totalAmount, string payload, CancellationToken ct = default)
    {
        if (currency != SponsorSales.Currency) return "Оплата принимается только звёздами.";
        if (SponsorSales.Parse(payload) is not { } order)
            return "Счёт повреждён — открой оплату заново.";
        // Сумма зашита в счёт при его создании. Расхождение значит, что счёт
        // собран не нами, и списывать по нему нельзя.
        if (order.Stars != totalAmount) return "Сумма не совпадает со счётом — открой оплату заново.";
        if (await ResolveAsync(order, ct) is null)
            return "Игрок не найден — привяжи профиль в боте и попробуй снова.";
        return null;
    }

    /// <summary>
    /// Кому выдавать. По номеру строки, если он в счёте, иначе по тегу.
    ///
    /// Тег не уникален, и первая оплата по нему ушла в чужую старую строку. Номер
    /// строки однозначен; по тегу ищем только для счетов старого формата, и там
    /// поиск уже предпочитает строку, привязанную к самому человеку.
    /// </summary>
    private Task<Player?> ResolveAsync(SponsorSales.Parsed order, CancellationToken ct) =>
        order.PlayerId is { } id
            ? players.GetByIdAsync(id, ct)
            : players.GetByTagAsync(order.PlayerTag!, ct);

    /// <param name="Duplicate">Этот платёж уже был учтён — повторная доставка.</param>
    public record Applied(string PlayerName, DateTime Until, int Days, bool Duplicate);

    /// <summary>
    /// Выдаёт спонсорство за прошедший платёж.
    ///
    /// null — выдать не удалось, хотя деньги списаны. Такое должно быть громким:
    /// вызывающий обязан записать это в лог с номером платежа, иначе вернуть
    /// человеку звёзды будет не по чему.
    /// </summary>
    public async Task<Applied?> ApplyAsync(
        long payerTelegramUserId, string payload, int totalAmount, string chargeId,
        CancellationToken ct = default)
    {
        if (SponsorSales.Parse(payload) is not { } order) return null;
        var days = order.Days;
        var stars = order.Stars;

        // Трекаемая выборка: через AsNoTracking-список изменение срока молча
        // не сохранилось бы — ровно так однажды уже терялась выдача спонсорства.
        var player = await ResolveAsync(order, ct);
        if (player is null) return null;

        if (await payments.ExistsAsync(chargeId, ct))
            return new Applied(player.Name, player.SponsorUntilUtc ?? DateTime.UtcNow, days, Duplicate: true);

        var now = DateTime.UtcNow;
        // Продление, а не перезапись: докупивший за неделю до конца не должен
        // терять оставшуюся неделю.
        var from = player.SponsorUntilUtc is { } until && until > now ? until : now;
        player.SponsorUntilUtc = from.AddDays(days);

        await payments.AddAsync(new SponsorPayment
        {
            PayerTelegramUserId = payerTelegramUserId,
            PlayerTag = player.PlayerTag,
            Stars = totalAmount > 0 ? totalAmount : stars,
            Days = days,
            TelegramChargeId = chargeId,
            PaidAtUtc = now,
        }, ct);

        // Спонсор получает весь Плюс - и дни складываются с уже купленным Плюсом, а
        // не перекрываются им: иначе купивший спонсорство поверх Плюса сжигал бы
        // оплаченные дни. Звёзды в выдаче - ноль: они уже учтены в платеже спонсорства.
        var recipientTg = player.TelegramUserId ?? payerTelegramUserId;
        await plus.GrantAsync(recipientTg, days, Entitlement.Sources.Sponsor, player.PlayerTag, 0, chargeId,
            ct, save: false);

        // Один контекст на оба репозитория: платёж и новый срок сохраняются вместе
        // или не сохраняются вовсе. Повтор того же платежа упрётся в уникальный
        // номер, и продление вместе с ним откатится.
        await players.SaveChangesAsync(ct);

        await HallCache.BumpAsync(settings, ct);
        return new Applied(player.Name, player.SponsorUntilUtc.Value, days, Duplicate: false);
    }
}
