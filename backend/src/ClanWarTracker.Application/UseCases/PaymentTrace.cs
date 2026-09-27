using System.Text.Json;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// След последней оплаты — для панели владельца.
///
/// Оплата проходит через бота в воркере, а смотрит владелец в панель API: это
/// разные контейнеры, и без записи в общую таблицу узнать, где именно сломалась
/// выдача, можно было только из логов сервера. Из-за этого первая поломка оплат
/// выглядела для владельца одинаково при любой причине: звёзды списались, а
/// дальше тишина.
///
/// Два шага пишутся отдельно, потому что ломаться они могут отдельно: запрос
/// «можно списывать?» и подтверждение «списано» приходят разными апдейтами.
/// Если первый записан, а второго нет — подтверждение до бота не доходит.
/// </summary>
public static class PaymentTrace
{
    public const string CheckoutKey = "pay.lastCheckout";
    public const string PaidKey = "pay.lastPaid";

    /// <param name="Outcome">Что вышло: «ok», причина отказа или текст ошибки.</param>
    public record Entry(DateTime AtUtc, string Payload, string? ChargeId, string Outcome);

    /// <summary>
    /// Записать шаг. Никогда не бросает: след не должен ломать саму оплату.
    ///
    /// Длины обрезаются под колонку значения настроек (2000 символов). Текст
    /// ошибки базы бывает длинным, и запись, упавшая на длине, спрятала бы ровно
    /// ту ошибку, ради которой её и пишут.
    /// </summary>
    public static async Task RecordAsync(
        IServiceSettingRepository settings, string key, string payload, string? chargeId, string outcome,
        CancellationToken ct = default)
    {
        try
        {
            var entry = new Entry(DateTime.UtcNow, Cut(payload, 120), chargeId is null ? null : Cut(chargeId, 400), Cut(outcome, 900));
            await settings.SetAsync(key, JsonSerializer.Serialize(entry), ct);
        }
        catch { /* диагностика не обязана работать, чтобы работала оплата */ }
    }

    public static async Task<Entry?> ReadAsync(IServiceSettingRepository settings, string key, CancellationToken ct = default)
    {
        var raw = await settings.GetAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return JsonSerializer.Deserialize<Entry>(raw); }
        catch { return null; }
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
