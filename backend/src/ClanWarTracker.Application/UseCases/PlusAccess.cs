using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Есть ли у человека «Clanify Плюс», откуда он и выдача нового срока.
///
/// Всё про доступ собрано здесь, а не размазано по экранам: разбор боёв, оповещения
/// и панель должны отвечать на «есть ли Плюс» одинаково. Иначе однажды разбор
/// покажет замок человеку, которому бот уже пишет как платному.
/// </summary>
public class PlusAccess(
    IEntitlementRepository entitlements,
    IPlayerRepository players,
    IServiceSettingRepository settings)
{
    /// <param name="Paywall">Платное включено. Выключено - всё открыто всем.</param>
    /// <param name="Active">Плюс действует: куплен, выдан, триал или спонсорство.</param>
    /// <param name="Until">До какого момента, если действует.</param>
    /// <param name="Source">Откуда: purchase, trial, gift, grant или sponsor.</param>
    /// <param name="TrialUsed">Триал уже был - второй раз не выдаётся.</param>
    public record Status(bool Paywall, bool Active, DateTime? Until, string? Source, bool TrialUsed)
    {
        /// <summary>Открыты ли платные функции: либо Плюс есть, либо платное выключено вовсе.</summary>
        public bool Unlocked => !Paywall || Active;
    }

    /// <summary>Откуда доступ, когда его даёт спонсорство.</summary>
    public const string SponsorSource = "sponsor";

    public async Task<Status> GetAsync(long telegramUserId, CancellationToken ct = default)
    {
        var offer = await PlusSales.ReadAsync(settings, ct);
        var now = DateTime.UtcNow;

        var latest = await entitlements.LatestAsync(telegramUserId, Entitlement.Skus.Plus, ct);
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);

        // Спонсор получает Плюс на весь срок спонсорства: он платит больше, и
        // требовать с него ещё и за разбор было бы странно.
        var sponsorUntil = player is not null && player.IsSponsor(now) ? player.SponsorUntilUtc : null;
        var bought = latest is not null && latest.UntilUtc > now ? latest.UntilUtc : (DateTime?)null;

        DateTime? until = (bought, sponsorUntil) switch
        {
            ({ } b, { } s) => b > s ? b : s,
            ({ } b, null) => b,
            (null, { } s) => s,
            _ => null,
        };
        var source = until is null
            ? null
            : sponsorUntil is { } su && su >= (bought ?? DateTime.MinValue) ? SponsorSource : latest!.Source;

        var trialUsed = await entitlements.HadTrialAsync(telegramUserId, player?.PlayerTag, Entitlement.Skus.Plus, ct);
        return new Status(offer.Paywall, until is not null, until, source, trialUsed);
    }

    /// <summary>
    /// Выдаёт триал, если он положен: платное включено, Плюса нет, триала ещё не
    /// было ни у этого аккаунта, ни у этого тега, и боёв уже достаточно, чтобы
    /// разбору было что показать. true - выдан только что.
    /// </summary>
    public async Task<bool> TryStartTrialAsync(
        long telegramUserId, string playerTag, int storedBattles, CancellationToken ct = default)
    {
        if (storedBattles < PlusSales.TrialMinBattles) return false;

        var offer = await PlusSales.ReadAsync(settings, ct);
        if (!offer.Paywall || offer.TrialDays <= 0) return false;

        var status = await GetAsync(telegramUserId, ct);
        if (status.Active || status.TrialUsed) return false;

        await GrantAsync(telegramUserId, offer.TrialDays, Entitlement.Sources.Trial, playerTag, 0, null, ct);
        return true;
    }

    /// <summary>
    /// Продлевает Плюс на <paramref name="days"/> дней от конца текущего срока (или от
    /// «сейчас», если срока нет). Возвращает новый срок.
    /// </summary>
    /// <param name="save">false - запись добавлена в контекст, сохранит вызывающий
    /// вместе со своими изменениями (оплата пишет платёж и доступ одной транзакцией).</param>
    public async Task<DateTime> GrantAsync(
        long telegramUserId, int days, string source, string? playerTag, int stars, string? chargeId,
        CancellationToken ct = default, bool save = true)
    {
        var now = DateTime.UtcNow;
        var current = await entitlements.UntilAsync(telegramUserId, Entitlement.Skus.Plus, ct);
        var from = current is { } c && c > now ? c : now;
        var until = from.AddDays(days);

        await entitlements.AddAsync(new Entitlement
        {
            TelegramUserId = telegramUserId,
            Sku = Entitlement.Skus.Plus,
            Source = source,
            Days = days,
            UntilUtc = until,
            PlayerTag = playerTag,
            Stars = stars,
            ChargeId = chargeId,
            CreatedAtUtc = now,
        }, ct);

        if (save) await entitlements.SaveChangesAsync(ct);
        return until;
    }
}
