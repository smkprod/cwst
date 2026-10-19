using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>Босс клана: урон считается из журналов, и ошибка тут - чужой Плюс или недобитый босс.</summary>
public class HalloweenTests
{
    private static readonly HalloweenUseCase.Boss Boss =
        new("vampire", T0, T0.AddDays(7), 600);

    private static WarBattle War(int minute, bool won, int crowns = 1, string tag = "#W1", string name = "Wolf") => new()
    {
        ClanId = 1,
        PlayerTag = tag,
        PlayerName = name,
        BattleTimeUtc = T0.AddMinutes(minute),
        Won = won,
        CrownsFor = crowns,
    };

    private static HalloweenUseCase.Progress Tally(int hp, IEnumerable<WarBattle> war, IEnumerable<PlayerBattle>? own = null,
        Dictionary<string, string>? names = null) =>
        HalloweenUseCase.Tally(Boss, hp, war, own ?? [], names ?? new(), T0.AddDays(30));

    [Fact]
    public void WarWins_HitAndThreeCrownsHitHarder()
    {
        var p = Tally(10_000, [War(1, true), War(2, true, crowns: 3), War(3, false)]);

        Assert.Equal(HalloweenUseCase.WarWin + HalloweenUseCase.WarThreeCrowns, p.Damage);
        var h = Assert.Single(p.Hitters);
        Assert.Equal("Wolf", h.Name);
        Assert.Equal(1, h.WarWins);
        Assert.Equal(1, h.ThreeCrowns);
    }

    [Fact]
    public void BattlesOutsideTheBossWindow_DoNotCount()
    {
        var p = Tally(10_000, [War(-5, true), War(7 * 24 * 60, true), War(10, true)]);

        Assert.Equal(HalloweenUseCase.WarWin, p.Damage);
    }

    [Fact]
    public void OwnBattles_OnlyLadderAndPathOfLegendsCount()
    {
        var own = new[] { Battle(1, 1), Battle(2, 1, "pathOfLegend"), Battle(3, 1, "riverRacePvP"), Battle(4, 1, "friendly"), Battle(5, -1) };

        var p = Tally(10_000, [], own, new() { ["#P1"] = "Kot" });

        // Бой КВ уже есть в журнале клана - из личного его не берём, чтобы не посчитать дважды
        Assert.Equal(2 * HalloweenUseCase.LadderWin, p.Damage);
        Assert.Equal("Kot", Assert.Single(p.Hitters).Name);
    }

    [Fact]
    public void LadderWins_CappedPerDay()
    {
        var own = Enumerable.Range(0, HalloweenUseCase.LadderWinsPerDay + 5).Select(i => Battle(i, 1)).ToList();

        var p = Tally(10_000, [], own);

        Assert.Equal(HalloweenUseCase.LadderWinsPerDay * HalloweenUseCase.LadderWin, p.Damage);
    }

    [Fact]
    public void Defeated_AtTheBattleThatCrossedHp()
    {
        var p = Tally(250, [War(1, true), War(2, true, tag: "#W2", name: "Ronin"), War(3, true)]);

        Assert.True(p.Defeated);
        Assert.Equal(T0.AddMinutes(3), p.DefeatedUtc);
        Assert.Equal(0, p.Left);
        Assert.Equal("Wolf", p.Hitters[0].Name);
    }

    [Fact]
    public void NotDefeated_WhileDamageBelowHp()
    {
        var p = Tally(1000, [War(1, true)]);

        Assert.False(p.Defeated);
        Assert.Equal(900, p.Left);
    }

    [Fact]
    public void Today_CountsOnlySinceTodayStart()
    {
        var p = HalloweenUseCase.Tally(Boss, 10_000, [War(1, true), War(60 * 30, true)], [], new Dictionary<string, string>(), T0.AddDays(1));

        Assert.Equal(2 * HalloweenUseCase.WarWin, p.Damage);
        Assert.Equal(HalloweenUseCase.WarWin, p.Today);
    }

    [Theory]
    [InlineData(0, 12_000)]   // журнала нет - средний клан
    [InlineData(3, 6_000)]    // маленький клан - не меньше десяти человек
    [InlineData(37, 22_500)]  // 37 * 600 = 22 200, вверх до 500
    [InlineData(80, 30_000)]  // больше пятидесяти не бывает
    public void Hp_ScalesWithActiveMembers(int active, int hp)
    {
        Assert.Equal(hp, HalloweenUseCase.HpFor(Boss, active));
    }

    [Fact]
    public void Vampire_HasBonusHpForTheWarItCameInto()
    {
        Assert.Equal(5000, HalloweenUseCase.Bosses[0].BonusHp);
        Assert.Equal(0, HalloweenUseCase.Bosses[1].BonusHp);
    }

    [Fact]
    public void Bosses_FollowEachOtherWithoutGaps()
    {
        var b = HalloweenUseCase.Bosses;
        for (var i = 1; i < b.Length; i++) Assert.Equal(b[i - 1].EndUtc, b[i].StartUtc);
        Assert.Equal(b[0], HalloweenUseCase.Live(b[0].StartUtc));
        Assert.Equal(b[1], HalloweenUseCase.Live(b[0].EndUtc));
        Assert.Null(HalloweenUseCase.Live(b[^1].EndUtc));
        Assert.Equal(b[0], HalloweenUseCase.Before(b[1]));
    }
}
