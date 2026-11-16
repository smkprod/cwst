using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using ClanWarTracker.Infrastructure.Persistence;
using ClanWarTracker.Infrastructure.Persistence.Repositories;

namespace ClanWarTracker.Tests;

/// <summary>История КВ игроков в чужих кланах - из журналов гонок наших кланов.</summary>
public class RaceLogTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private static List<RiverRaceLogWeek> Log() =>
    [
        new()
        {
            SeasonId = 140, SectionIndex = 1,
            Standings =
            [
                new() { Rank = 1, ClanTag = "#OURS", ClanName = "Наш", Fame = 30000,
                    Participants = [new() { PlayerTag = "#A", Name = "A", Fame = 3000, DecksUsed = 16 }] },
                new() { Rank = 2, ClanTag = "#OTHER", ClanName = "Чужой клан", Fame = 20000,
                    Participants =
                    [
                        new() { PlayerTag = "#B", Name = "B", Fame = 2400, DecksUsed = 12 },
                        new() { PlayerTag = "#C", Name = "C", Fame = 0, DecksUsed = 0 },
                    ] },
            ],
        },
    ];

    private static CaptureWarSnapshotsUseCase Build(AppDbContext ctx)
    {
        var api = Stub<IClashRoyaleApi>.Make(s => s.On["GetRiverRaceLogAsync"] = _ => Task.FromResult(Log()));
        return new CaptureWarSnapshotsUseCase(api, new ClanRepository(ctx), new WarSnapshotRepository(ctx),
            new PlayerRepository(ctx), new RaceTickRepository(ctx), new RaceLogRepository(ctx));
    }

    [Fact]
    public async Task Capture_StoresAllRaceClans_Once()
    {
        await using var ctx = _db.New();
        ctx.Clans.Add(new Clan { ClanTag = "#OURS", Name = "Наш" });
        await ctx.SaveChangesAsync();

        await Build(ctx).ExecuteAsync();
        await Build(ctx).ExecuteAsync();

        var repo = new RaceLogRepository(ctx);
        var b = Assert.Single(await repo.GetForPlayerAsync("#B", 10));
        Assert.Equal(("#OTHER", "Чужой клан", 2400, 12, 20000, 12), (b.ClanTag, b.ClanName, b.Fame, b.DecksUsed, b.ClanFame, b.ClanDecks));
        Assert.Single(await repo.GetForPlayerAsync("#A", 10));
        // Не сыгравших не пишем
        Assert.Empty(await repo.GetForPlayerAsync("#C", 10));
    }
}
