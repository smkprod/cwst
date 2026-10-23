using ClanWarTracker.Application.UseCases;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>Профиль-дашборд: серии, путь кубков и карты - то, что игрок сверяет со своей памятью.</summary>
public class DashboardTests
{
    [Fact]
    public void Streaks_BestWinAndWorstLoss()
    {
        var list = new[] { Battle(0, 1), Battle(1, 1), Battle(2, 1), Battle(3, -1), Battle(4, -1), Battle(5, 1) };

        var (bestWin, worstLoss) = GetPlayerDashboardUseCase.Streaks(list);

        Assert.Equal(3, bestWin);
        Assert.Equal(2, worstLoss);
        Assert.Equal(1, GetPlayerDashboardUseCase.CurrentStreak(list));
        Assert.Equal(-2, GetPlayerDashboardUseCase.CurrentStreak(list.Take(5)));
    }

    [Fact]
    public void TrophySeries_GoesBackFromCurrentTrophies()
    {
        var a = Battle(0, 1); a.TrophyChange = 30;
        var b = Battle(1, -1); b.TrophyChange = -28;
        var c = Battle(2, 1); c.TrophyChange = 31;

        var s = GetPlayerDashboardUseCase.TrophySeries([a, b, c], 5000);

        Assert.Equal([4967, 4997, 4969, 5000], s);
        Assert.Empty(GetPlayerDashboardUseCase.TrophySeries([a, b, c], null));
    }

    [Fact]
    public void CardRates_CountEvoAsSameCard()
    {
        var list = new[]
        {
            Battle(0, 1, deck: "-1,2,3,4,5,6,7,8"), Battle(1, 1, deck: "1,2,3,4,5,6,7,8"), Battle(2, -1, deck: "1,2,3,4,5,6,7,9"),
        };

        var rates = GetPlayerDashboardUseCase.CardRates(list);

        var one = Assert.Single(rates, r => r.Key == 1);
        Assert.Equal(3, one.Games);
        Assert.Equal(2, one.Wins);
        Assert.DoesNotContain(rates, r => r.Key == 9);
    }

    [Theory]
    [InlineData("Hog Rider", "anti_hog")]
    [InlineData("Golem", "anti_tank")]
    [InlineData("Mega Knight", "kite")]
    [InlineData("Goblin Barrel", "log_barrel")]
    [InlineData("Balloon", "anti_air")]
    [InlineData("X-Bow", "anti_siege")]
    [InlineData("Miner", "anti_miner")]
    [InlineData("Knight", null)]
    public void TipFor_Card(string card, string? tip)
    {
        Assert.Equal(tip, GetPlayerDashboardUseCase.TipFor(card));
    }
}
