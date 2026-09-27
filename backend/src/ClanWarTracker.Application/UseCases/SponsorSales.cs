using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Условия продажи спонсорства: цена в звёздах и срок.
///
/// Лежат в настройках, а не в коде: цену меняют чаще, чем выкатывают сборку, и
/// ради «поставить 250 вместо 200» пересобирать образ было бы нелепо. Ноль в
/// цене означает «продажа выключена» — тогда приложение показывает прежнюю
/// кнопку «написать владельцу», а не кнопку оплаты, которая откажет.
/// </summary>
public static class SponsorSales
{
    public const string PriceKey = "sponsor.priceStars";
    public const string DaysKey = "sponsor.days";

    public const int DefaultDays = 30;

    /// <summary>Потолок цены — защита от лишнего нуля, набранного в панели.</summary>
    public const int MaxStars = 100_000;
    public const int MaxDays = 366;

    /// <summary>Валюта звёзд Telegram. Для неё провайдер не нужен.</summary>
    public const string Currency = "XTR";

    public record Offer(int Stars, int Days)
    {
        public bool OnSale => Stars > 0 && Days > 0;
    }

    public static async Task<Offer> ReadAsync(IServiceSettingRepository settings, CancellationToken ct)
    {
        var stars = int.TryParse(await settings.GetAsync(PriceKey, ct), out var s) ? s : 0;
        var days = int.TryParse(await settings.GetAsync(DaysKey, ct), out var d) ? d : DefaultDays;
        return new Offer(Math.Clamp(stars, 0, MaxStars), Math.Clamp(days, 1, MaxDays));
    }

    /// <summary>
    /// Что зашито в счёт: кому, на сколько дней и за сколько звёзд.
    ///
    /// Цена и срок едут в самом счёте, а не читаются из настроек в момент оплаты:
    /// владелец может поменять цену, пока человек держит счёт открытым, и тогда
    /// честно выдать ровно то, что было написано на кнопке, за ровно ту сумму,
    /// которую он согласился заплатить.
    /// </summary>
    public static string Payload(string playerTag, int days, int stars) =>
        $"sp|{playerTag}|{days}|{stars}";

    public static bool TryParse(string? payload, out string playerTag, out int days, out int stars)
    {
        playerTag = "";
        days = stars = 0;
        var parts = payload?.Split('|');
        if (parts is not ["sp", var tag, var d, var s]) return false;
        if (!int.TryParse(d, out days) || !int.TryParse(s, out stars)) return false;
        if (string.IsNullOrWhiteSpace(tag) || days <= 0 || stars <= 0) return false;
        playerTag = tag;
        return true;
    }
}
