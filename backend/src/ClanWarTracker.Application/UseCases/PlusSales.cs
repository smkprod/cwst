using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Условия продажи «Clanify Плюс»: цены пропусков, триал и общий выключатель.
///
/// Как и у спонсорства, всё лежит в настройках, а не в коде: цену и выключатель
/// владелец меняет из панели, без передеплоя. Выключатель нужен не для красоты:
/// если придёт претензия или что-то сломается в оплате, платное закрывается одним
/// нажатием, и всё платное просто становится бесплатным для всех.
/// </summary>
public static class PlusSales
{
    public const string PaywallKey = "plus.paywall";
    public const string Price7Key = "plus.price7";
    public const string Price30Key = "plus.price30";

    /// <summary>
    /// Цены совпадают с пачками звёзд в Telegram (50 и 100): купить меньше пачки
    /// нельзя, и лишние звёзды на счету у школьника - это недоплаченная покупка.
    /// 149 для украинского подростка оказалось слишком много.
    /// </summary>
    public const int DefaultPrice7 = 49;
    public const int DefaultPrice30 = 99;

    /// <summary>Сколько бесплатных недель в месяц спонсор дарит соклановцам.</summary>
    public const int SponsorFreeGiftsPerMonth = 2;
    public const int SponsorFreeGiftDays = 7;

    public const int MaxStars = 10_000;

    /// <summary>Срок пропусков. Только эти два: счёт с другим сроком собран не нами.</summary>
    public static readonly int[] PassDays = [7, 30];

    public record Offer(bool Paywall, int Price7, int Price30)
    {
        public int PriceFor(int days) => days switch { 7 => Price7, 30 => Price30, _ => 0 };
        public bool OnSale => Paywall && Price7 > 0 && Price30 > 0;
    }

    public static async Task<Offer> ReadAsync(IServiceSettingRepository settings, CancellationToken ct)
    {
        // Выключатель по умолчанию включён: без записи в настройках платное работает.
        var paywall = !string.Equals(await settings.GetAsync(PaywallKey, ct), "off", StringComparison.OrdinalIgnoreCase);
        return new Offer(
            paywall,
            await IntAsync(settings, Price7Key, DefaultPrice7, ct),
            await IntAsync(settings, Price30Key, DefaultPrice30, ct));
    }

    private static async Task<int> IntAsync(IServiceSettingRepository settings, string key, int fallback, CancellationToken ct) =>
        int.TryParse(await settings.GetAsync(key, ct), out var v) ? Math.Clamp(v, 0, MaxStars) : fallback;

    /// <summary>
    /// Что зашито в счёт: кому (аккаунт Telegram), на сколько дней и за сколько звёзд.
    /// Префикс «pl» отличает счёт Плюса от спонсорского «sp»: оба приходят в один
    /// и тот же обработчик оплаты.
    /// </summary>
    public static string Payload(long recipientTelegramUserId, int days, int stars) =>
        $"pl|{recipientTelegramUserId}|{days}|{stars}";

    public record Parsed(long RecipientTelegramUserId, int Days, int Stars);

    public static bool IsPlusPayload(string? payload) => payload?.StartsWith("pl|", StringComparison.Ordinal) == true;

    public static Parsed? Parse(string? payload)
    {
        var parts = payload?.Split('|');
        if (parts is not ["pl", var who, var d, var s]) return null;
        if (!long.TryParse(who, out var tg) || tg <= 0) return null;
        if (!int.TryParse(d, out var days) || !int.TryParse(s, out var stars)) return null;
        if (Array.IndexOf(PassDays, days) < 0 || stars <= 0) return null;
        return new Parsed(tg, days, stars);
    }
}
