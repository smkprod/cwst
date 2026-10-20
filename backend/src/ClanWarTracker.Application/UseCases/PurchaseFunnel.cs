using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Воронка покупки для панели владельца: сколько человек увидели предложение, сколько
/// нажали «Купить» и сколько заплатили - по Плюсу и спонсорству отдельно.
///
/// Без неё «не покупают» не разобрать: не видят, видят и не жмут, жмут и не платят -
/// это три разные болезни, и лечатся они по-разному.
/// </summary>
public class PurchaseFunnel(IFunnelRepository funnel, ISponsorPaymentRepository payments)
{
    /// <summary>Отметить шаг. Никогда не бросает: статистика не должна ломать покупку.</summary>
    public static async Task MarkAsync(IFunnelRepository funnel, long telegramUserId, string product, string step,
        string place, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            await funnel.AddAsync(new FunnelEvent
            {
                TelegramUserId = telegramUserId,
                DayUtc = now.ToString("yyyy-MM-dd"),
                Product = product,
                Step = step,
                Place = place,
                FirstUtc = now,
            }, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested) { /* без отметки покупка всё равно идёт */ }
    }

    /// <param name="Seen">Сколько разных людей увидели предложение.</param>
    /// <param name="Buy">Сколько нажали «Купить» - им выставлен счёт.</param>
    /// <param name="Paid">Сколько заплатили.</param>
    public record Totals(int Seen, int Buy, int Paid);

    public record ProductRow(string Product, Totals Week, Totals Month);

    public record DayRow(string Day, string Product, int Seen, int Buy, int Paid);

    /// <summary>Где увидели: окно покупки, витрина, витрина вкладкой.</summary>
    public record PlaceRow(string Product, string Place, int Users30);

    /// <param name="TrackingSince">С какого дня пишутся шаги; null - отметок ещё не было.</param>
    public record Dto(List<ProductRow> Products, List<DayRow> Days, List<PlaceRow> Places, string? TrackingSince);

    public async Task<Dto> ExecuteAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        string Day(int back) => today.AddDays(-back).ToString("yyyy-MM-dd");
        var monthStart = Day(29);
        var weekStart = Day(6);

        var steps = await funnel.GetSinceAsync(monthStart, ct);
        var since = await funnel.FirstDayAsync(ct);
        // Журнал оплат общий на оба товара: вид оплаты и есть товар воронки. Возвраты не считаем.
        // Оплаты - только с первого дня отметок: старые оплаты против пустых «увидели» и
        // «нажали» дали бы «400%» и ложную подсказку.
        var paidFrom = since is not null && string.CompareOrdinal(since, monthStart) > 0 ? since : monthStart;
        var paid = since is null ? [] : (await payments.GetAllAsync(ct))
            .Where(p => p.RefundedAtUtc is null)
            .Select(p => new Payment(p.Kind, p.PaidAtUtc.ToString("yyyy-MM-dd"), p.PayerTelegramUserId))
            .Where(p => string.CompareOrdinal(p.Day, paidFrom) >= 0)
            .ToList();

        Totals Count(string product, string since, string? until = null)
        {
            bool In(string day) => string.CompareOrdinal(day, since) >= 0 && (until is null || string.CompareOrdinal(day, until) <= 0);
            int Users(string step) => steps.Where(s => s.Product == product && s.Step == step && In(s.DayUtc))
                .Select(s => s.TelegramUserId).Distinct().Count();
            var payers = paid.Where(p => p.Product == product && In(p.Day)).Select(p => p.User).Distinct().Count();
            return new Totals(Users(FunnelEvent.Steps.Seen), Users(FunnelEvent.Steps.Buy), payers);
        }

        var products = FunnelEvent.Products.All
            .Select(p => new ProductRow(p, Count(p, weekStart), Count(p, monthStart)))
            .ToList();

        var days = new List<DayRow>();
        for (var back = 13; back >= 0; back--)
        {
            var d = Day(back);
            foreach (var p in FunnelEvent.Products.All)
            {
                var t = Count(p, d, d);
                days.Add(new DayRow(d, p, t.Seen, t.Buy, t.Paid));
            }
        }

        var places = steps
            .Where(s => s.Step == FunnelEvent.Steps.Seen)
            .GroupBy(s => (s.Product, s.Place))
            .Select(g => new PlaceRow(g.Key.Product, g.Key.Place, g.Select(s => s.TelegramUserId).Distinct().Count()))
            .OrderByDescending(p => p.Users30)
            .ToList();

        return new Dto(products, days, places, since);
    }

    private record Payment(string Product, string Day, long User);
}
