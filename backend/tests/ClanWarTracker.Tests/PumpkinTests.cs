using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Infrastructure.Persistence;
using ClanWarTracker.Infrastructure.Persistence.Repositories;

namespace ClanWarTracker.Tests;

/// <summary>«Охота на тыквы»: находки один раз, награда ровно за тринадцатую.</summary>
public class PumpkinTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private static PumpkinHuntUseCase Build(AppDbContext ctx)
    {
        var players = new PlayerRepository(ctx);
        return new PumpkinHuntUseCase(new PumpkinRepository(ctx), players,
            new PlusAccess(new EntitlementRepository(ctx), players, new ServiceSettingRepository(ctx)));
    }

    [Fact]
    public async Task Find_AllThirteen_GrantsPlusOnce()
    {
        if (!PumpkinHuntUseCase.Open(DateTime.UtcNow)) return; // вне ивента находки закрыты
        await using var ctx = _db.New();
        ctx.Players.Add(new Player { PlayerTag = "#P1", Name = "Охотник", TelegramUserId = 5 });
        await ctx.SaveChangesAsync();
        var uc = Build(ctx);

        for (var i = 1; i < PumpkinHuntUseCase.Total; i++)
            Assert.False((await uc.FindAsync(5, i))!.Completed);
        Assert.False((await uc.FindAsync(5, 1))!.IsNew);

        var last = await uc.FindAsync(5, PumpkinHuntUseCase.Total);
        Assert.True(last!.Completed);
        Assert.NotNull(last.RewardUntil);
        // Повтор последней - уже не награда
        Assert.False((await uc.FindAsync(5, PumpkinHuntUseCase.Total))!.Completed);

        var status = await uc.StatusAsync(5);
        Assert.Equal(PumpkinHuntUseCase.Total, status.Found.Count);
        Assert.Equal(1, status.Finishers);
        Assert.Equal(["Охотник"], status.FirstFinishers);
    }

    [Fact]
    public async Task Find_UnknownPumpkin_Refused()
    {
        await using var ctx = _db.New();
        Assert.Null(await Build(ctx).FindAsync(5, 0));
        Assert.Null(await Build(ctx).FindAsync(5, PumpkinHuntUseCase.Total + 1));
    }

    [Fact]
    public void Open_OnlyDuringEvent()
    {
        Assert.False(PumpkinHuntUseCase.Open(PumpkinHuntUseCase.StartUtc.AddMinutes(-1)));
        Assert.True(PumpkinHuntUseCase.Open(PumpkinHuntUseCase.StartUtc));
        Assert.False(PumpkinHuntUseCase.Open(PumpkinHuntUseCase.EndUtc));
    }
}
