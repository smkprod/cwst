using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using ClanWarTracker.Infrastructure.Persistence;
using ClanWarTracker.Infrastructure.Persistence.Repositories;

namespace ClanWarTracker.Tests;

/// <summary>Оплата через банку Монобанка: код в комментарии, выписка, выдача доступа.</summary>
public class JarTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private const string Link = "https://send.monobank.ua/jar/8AbcDEF12";
    private const long Tg = 777;

    [Theory]
    [InlineData("482913", new[] { "482913" })]
    [InlineData("код 482 913, спасибо", new[] { "482913" })]
    [InlineData("482-913", new[] { "482913" })]
    [InlineData("за плюс 1234567", new string[0])]
    [InlineData("012345", new string[0])]
    [InlineData(null, new string[0])]
    public void CodesIn_FindsSixDigitCodes(string? comment, string[] expected) =>
        Assert.Equal(expected, JarSales.CodesIn(comment));

    [Theory]
    [InlineData("send.monobank.ua/jar/8AbcDEF12", "https://send.monobank.ua/jar/8AbcDEF12")]
    [InlineData("https://send.monobank.ua/jar/8AbcDEF12/", "https://send.monobank.ua/jar/8AbcDEF12")]
    [InlineData("https://evil.example/jar/8AbcDEF12", null)]
    [InlineData("http://send.monobank.ua/jar/8AbcDEF12", null)]
    [InlineData("https://send.monobank.ua/", null)]
    public void NormalizeLink_OnlyMonobankJar(string raw, string? expected) =>
        Assert.Equal(expected, JarSales.NormalizeLink(raw));

    [Fact]
    public void SameJar_MatchesApiSendIdWithOrWithoutPrefix()
    {
        Assert.True(JarSales.SameJar("jar/8AbcDEF12", "8AbcDEF12"));
        Assert.True(JarSales.SameJar("8AbcDEF12", "8AbcDEF12"));
        Assert.False(JarSales.SameJar("jar/other", "8AbcDEF12"));
    }

    private async Task SeedAsync(AppDbContext ctx)
    {
        ctx.Players.Add(new Player { PlayerTag = "#P1", Name = "Тест", TelegramUserId = Tg });
        await ctx.SaveChangesAsync();
        var settings = new ServiceSettingRepository(ctx);
        await new JarPaymentUseCase(new JarRepository(ctx), settings, Mono([]), new PlusAccess(new EntitlementRepository(ctx),
                new PlayerRepository(ctx), settings), new PlayerRepository(ctx), new SponsorPaymentRepository(ctx),
                new PlayerAlertPrefsRepository(ctx), Stub<INotificationSender>.Make(), new FunnelRepository(ctx))
            .SaveSettingsAsync(true, Link, 39, 79, 249);
    }

    private static IMonobankClient Mono(List<MonoTx> txs) => Stub<IMonobankClient>.Make(s =>
    {
        s.On["get_Configured"] = _ => true;
        s.On["JarsAsync"] = _ => Task.FromResult<IReadOnlyList<MonoJar>>([new MonoJar("acc1", "jar/8AbcDEF12", "Clanify")]);
        s.On["StatementAsync"] = _ => Task.FromResult<IReadOnlyList<MonoTx>>(txs);
    });

    private static JarPaymentUseCase Build(AppDbContext ctx, IMonobankClient mono)
    {
        var settings = new ServiceSettingRepository(ctx);
        var players = new PlayerRepository(ctx);
        return new JarPaymentUseCase(new JarRepository(ctx), settings, mono,
            new PlusAccess(new EntitlementRepository(ctx), players, settings), players, new SponsorPaymentRepository(ctx),
            new PlayerAlertPrefsRepository(ctx), Stub<INotificationSender>.Make(), new FunnelRepository(ctx));
    }

    [Fact]
    public async Task Order_SameOpenOrder_KeepsCode()
    {
        await using var ctx = _db.New();
        await SeedAsync(ctx);
        var uc = Build(ctx, Mono([]));
        var (a, _) = await uc.CreateOrderAsync(Tg, "plus", 30);
        var (b, _) = await uc.CreateOrderAsync(Tg, "plus", 30);
        Assert.NotNull(a);
        Assert.Equal(a!.Code, b!.Code);
        Assert.Equal(79, a.AmountUah);
        Assert.Equal(Link, a.Link);
    }

    [Fact]
    public async Task Tick_CodeInComment_GrantsPlusOnce()
    {
        await using var ctx = _db.New();
        await SeedAsync(ctx);
        var (order, _) = await Build(ctx, Mono([])).CreateOrderAsync(Tg, "plus", 30);
        var tx = new MonoTx("tx1", DateTime.UtcNow, 7900, $"Код {order!.Code}", "Від: Тест");

        var uc = Build(ctx, Mono([tx]));
        // Первый такт находит банку по ссылке, выписку читает второй
        Assert.Equal(0, (await uc.TickAsync()).Paid);
        Assert.Equal(1, (await uc.TickAsync()).Paid);
        // Та же операция в следующей выписке - не второй раз
        Assert.Equal(0, (await uc.TickAsync()).Paid);

        var until = await new EntitlementRepository(ctx).UntilAsync(Tg, Entitlement.Skus.Plus);
        Assert.NotNull(until);
        Assert.InRange(until!.Value, DateTime.UtcNow.AddDays(29.9), DateTime.UtcNow.AddDays(30.1));
        Assert.True((await uc.GetOrderAsync(Tg, order.Code))!.Paid);
        Assert.Null(await uc.GetOrderAsync(Tg + 1, order.Code));
    }

    [Fact]
    public async Task Tick_Underpaid_StaysForOwner_ThenManualConfirm()
    {
        await using var ctx = _db.New();
        await SeedAsync(ctx);
        var (order, _) = await Build(ctx, Mono([])).CreateOrderAsync(Tg, "pro", 0);
        var uc = Build(ctx, Mono([new MonoTx("tx2", DateTime.UtcNow, 10000, order!.Code, "Від: Тест")]));
        await uc.TickAsync();
        Assert.Equal(0, (await uc.TickAsync()).Paid);

        var view = await uc.OwnerViewAsync();
        var incoming = Assert.Single(view.Unmatched);
        Assert.Equal(10000, incoming.AmountKop);

        Assert.Equal((true, null), await uc.ConfirmAsync(order.Code, incoming.Id));
        Assert.Empty((await uc.OwnerViewAsync()).Unmatched);
        var player = await new PlayerRepository(ctx).GetByTelegramIdAsync(Tg);
        Assert.True(player!.SponsorUntilUtc > DateTime.UtcNow.AddDays(29));
        Assert.Equal((false, "already_paid"), await uc.ConfirmAsync(order.Code, null));
    }

    [Fact]
    public async Task Order_JarOff_Refused()
    {
        await using var ctx = _db.New();
        var (order, error) = await Build(ctx, Mono([])).CreateOrderAsync(Tg, "plus", 30);
        Assert.Null(order);
        Assert.Equal("jar_off", error);
    }
}
