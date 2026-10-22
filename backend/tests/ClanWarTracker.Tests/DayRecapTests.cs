using ClanWarTracker.Application.Battles;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>Итог дня: картинка уходит в личку каждое утро, ошибка в счёте видна сразу.</summary>
public class DayRecapTests
{
    [Fact]
    public void CountsScoreStreakAndThreeCrowns()
    {
        var day = new[]
        {
            Battle(0, 1), Battle(1, 1, crownsFor: 3), Battle(2, -1), Battle(3, 1), Battle(4, 1), Battle(5, 1), Battle(6, 0),
        };
        day[0].TrophyChange = 30; day[2].TrophyChange = -28;

        var r = DayRecap.Build(day);

        Assert.Equal(5, r.Wins);
        Assert.Equal(1, r.Losses);
        Assert.Equal(1, r.Draws);
        Assert.Equal(3, r.BestStreak);
        Assert.Equal(1, r.ThreeCrowns);
        Assert.Equal(2, r.Trophies);
        Assert.Equal([1, 1, -1, 1, 1, 1, 0], r.Results);
        Assert.Equal(71, r.WinPercent);
    }

    [Fact]
    public void OnlyLadderAndPathOfLegends()
    {
        var r = DayRecap.Build([Battle(0, 1, "riverRacePvP"), Battle(1, 1, "friendly"), Battle(2, 1, "pathOfLegend")]);

        Assert.Equal(1, r.Battles);
    }

    [Fact]
    public void Window_YesterdayIsLocalDay()
    {
        // 01:00 по Киеву 22-го: «вчера» - весь местный день 21-го
        var now = new DateTime(2026, 10, 21, 22, 0, 0, DateTimeKind.Utc);
        var (from, to, date) = DayRecap.Window(now, 180, 1);

        Assert.Equal(new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(from.AddDays(1), to);
        Assert.Equal(new DateTime(2026, 10, 21), date);
    }
}
