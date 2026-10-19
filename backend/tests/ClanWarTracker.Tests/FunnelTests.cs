using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Infrastructure.Persistence.Repositories;

namespace ClanWarTracker.Tests;

/// <summary>Воронка покупки: по ней владелец решает, что чинить - видимость, ценность или оплату.</summary>
public class FunnelTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private async Task MarkAsync(long tg, string product, string step, string place, int daysAgo = 0)
    {
        await using var db = _db.New();
        await new FunnelRepository(db).AddAsync(new FunnelEvent
        {
            TelegramUserId = tg,
            DayUtc = Now.AddDays(-daysAgo).ToString("yyyy-MM-dd"),
            Product = product,
            Step = step,
            Place = place,
            FirstUtc = Now.AddDays(-daysAgo),
        });
    }

    private async Task PayAsync(long tg, string kind, int daysAgo = 0, bool refunded = false)
    {
        await using var db = _db.New();
        db.SponsorPayments.Add(new SponsorPayment
        {
            PayerTelegramUserId = tg,
            PlayerTag = "#P",
            Stars = 99,
            Days = 30,
            TelegramChargeId = $"c{tg}{kind}{daysAgo}",
            PaidAtUtc = Now.AddDays(-daysAgo),
            Kind = kind,
            RefundedAtUtc = refunded ? Now : null,
        });
        await db.SaveChangesAsync();
    }

    private async Task<PurchaseFunnel.Dto> RunAsync()
    {
        await using var db = _db.New();
        return await new PurchaseFunnel(new FunnelRepository(db), new SponsorPaymentRepository(db)).ExecuteAsync(Now);
    }

    [Fact]
    public async Task CountsDistinctPeoplePerStep()
    {
        // Двое увидели Плюс (один - дважды и в двух местах), один нажал «Купить», один заплатил
        await MarkAsync(1, "plus", "seen", "sheet");
        await MarkAsync(1, "plus", "seen", "sheet");
        await MarkAsync(1, "plus", "seen", "showcase");
        await MarkAsync(2, "plus", "seen", "tab");
        await MarkAsync(1, "plus", "buy", "invoice");
        await PayAsync(1, SponsorPayment.Kinds.Plus);

        var plus = (await RunAsync()).Products.Single(p => p.Product == "plus");

        Assert.Equal(new PurchaseFunnel.Totals(2, 1, 1), plus.Week);
        Assert.Equal(new PurchaseFunnel.Totals(2, 1, 1), plus.Month);
    }

    [Fact]
    public async Task PaymentsSplitByKind_RefundsIgnored()
    {
        await PayAsync(1, SponsorPayment.Kinds.Sponsor);
        await PayAsync(2, SponsorPayment.Kinds.Plus);
        await PayAsync(3, SponsorPayment.Kinds.Sponsor, refunded: true);

        var r = await RunAsync();

        Assert.Equal(1, r.Products.Single(p => p.Product == "sponsor").Month.Paid);
        Assert.Equal(1, r.Products.Single(p => p.Product == "plus").Month.Paid);
    }

    [Fact]
    public async Task WeekAndMonthWindows()
    {
        await MarkAsync(1, "sponsor", "seen", "sheet", daysAgo: 10);
        await MarkAsync(2, "sponsor", "seen", "sheet", daysAgo: 2);
        await MarkAsync(3, "sponsor", "seen", "sheet", daysAgo: 40);

        var sp = (await RunAsync()).Products.Single(p => p.Product == "sponsor");

        Assert.Equal(1, sp.Week.Seen);
        Assert.Equal(2, sp.Month.Seen);
    }

    [Fact]
    public async Task DaysCoverTwoWeeksForBothProducts_AndPlacesAreCounted()
    {
        await MarkAsync(1, "plus", "seen", "showcase", daysAgo: 1);
        await MarkAsync(2, "plus", "seen", "showcase", daysAgo: 1);

        var r = await RunAsync();

        Assert.Equal(28, r.Days.Count);
        Assert.Equal(2, r.Days.Single(d => d.Product == "plus" && d.Day == "2026-10-18").Seen);
        Assert.Equal(2, r.Places.Single(p => p.Product == "plus" && p.Place == "showcase").Users30);
        Assert.Equal("2026-10-18", r.TrackingSince);
    }
}
