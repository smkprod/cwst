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
    ///
    /// Получатель - номер строки, а не тег. Тег не уникален: у игрока по строке на
    /// каждый клан, где он бывал, и первая же оплата по тегу записала спонсорство
    /// в чужую старую строку. Звёзды списались, а звезды у игрока не появилось.
    /// </summary>
    public static string Payload(int playerId, int days, int stars) =>
        $"sp|{playerId}|{days}|{stars}";

    /// <param name="PlayerId">Строка игрока. null - счёт старого формата, где вместо неё тег.</param>
    /// <param name="PlayerTag">Тег из счёта старого формата.</param>
    public record Parsed(int? PlayerId, string? PlayerTag, int Days, int Stars);

    /// <summary>
    /// Разбор счёта. Понимает и старый формат с тегом: счета, выставленные до
    /// перехода на номер строки, ещё могут быть открыты у людей, и оплата по ним
    /// обязана пройти, а не упасть на «счёт повреждён».
    /// </summary>
    public static Parsed? Parse(string? payload)
    {
        var parts = payload?.Split('|');
        if (parts is not ["sp", var who, var d, var s]) return null;
        if (!int.TryParse(d, out var days) || !int.TryParse(s, out var stars)) return null;
        if (days <= 0 || stars <= 0) return null;

        if (int.TryParse(who, out var id) && id > 0) return new Parsed(id, null, days, stars);
        if (who.Length > 1 && who[0] == '#') return new Parsed(null, who, days, stars);
        return null;
    }
}
