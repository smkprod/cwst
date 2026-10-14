using ClanWarTracker.Application.Battles;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>«Стоп-тильт» пишет человеку посреди игры: ложный сигнал раздражает, пропущенный - стоит кубков.</summary>
public class TiltTests
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(15);

    [Fact]
    public void TwoLossesInARow_RightNow_Alerts()
    {
        var battles = new[] { Battle(0, 1), Battle(5, -1), Battle(10, -1) };

        var check = BattleAnalyzer.ShouldAlertTilt(battles, T0.AddMinutes(12), null, Fresh);

        Assert.True(check.Alert);
        Assert.Equal(2, check.LossStreak);
    }

    [Fact]
    public void LastBattleTooOld_NoAlert()
    {
        var battles = new[] { Battle(0, -1), Battle(5, -1) };

        Assert.False(BattleAnalyzer.ShouldAlertTilt(battles, T0.AddMinutes(60), null, Fresh).Alert);
    }

    [Fact]
    public void WinAfterLosses_ResetsStreak()
    {
        var battles = new[] { Battle(0, -1), Battle(5, -1), Battle(10, 1) };

        var check = BattleAnalyzer.ShouldAlertTilt(battles, T0.AddMinutes(11), null, Fresh);

        Assert.False(check.Alert);
        Assert.Equal(0, check.LossStreak);
    }

    [Fact]
    public void AlreadyAlertedThisSession_NoSecondAlert()
    {
        var battles = new[] { Battle(0, -1), Battle(5, -1), Battle(10, -1) };

        var check = BattleAnalyzer.ShouldAlertTilt(battles, T0.AddMinutes(11), lastAlertBattleUtc: T0.AddMinutes(5), Fresh);

        Assert.False(check.Alert);
    }

    [Fact]
    public void LossesFromPreviousSession_DoNotCount()
    {
        // Два поражения утром, перерыв больше получаса, одно поражение сейчас
        var battles = new[] { Battle(0, -1), Battle(3, -1), Battle(120, -1) };

        var check = BattleAnalyzer.ShouldAlertTilt(battles, T0.AddMinutes(121), null, Fresh);

        Assert.False(check.Alert);
        Assert.Equal(1, check.LossStreak);
    }

    [Fact]
    public void Threshold_IsRespected()
    {
        var battles = new[] { Battle(0, -1), Battle(5, -1) };

        Assert.False(BattleAnalyzer.ShouldAlertTilt(battles, T0.AddMinutes(6), null, Fresh, threshold: 3).Alert);
    }

    [Fact]
    public void SessionAround_SplitsOnLongGap()
    {
        var battles = new[] { Battle(0, 1), Battle(10, 1), Battle(100, -1), Battle(110, 1) };

        var session = BattleAnalyzer.SessionAround(battles, T0.AddMinutes(110));

        Assert.Equal(2, session.Count);
        Assert.Equal(T0.AddMinutes(100), session[0].BattleTimeUtc);
    }

    [Fact]
    public void SessionAround_UnknownAnchor_Empty()
    {
        Assert.Empty(BattleAnalyzer.SessionAround([Battle(0, 1)], T0.AddMinutes(5)));
    }

    [Theory]
    [InlineData("PvP", true)]
    [InlineData("pathOfLegend", true)]
    [InlineData("riverRacePvP", false)]
    [InlineData("friendly", false)]
    [InlineData(null, false)]
    public void TiltEligibleModes(string? type, bool expected)
    {
        Assert.Equal(expected, BattleAnalyzer.IsTiltEligible(type));
    }

    [Fact]
    public void Profile_TooFewGames_NoType()
    {
        var profile = BattleAnalyzer.Profile([Battle(0, -1), Battle(1, -1), Battle(2, 1)]);

        Assert.Null(profile.Type);
        Assert.Null(profile.After2Percent);
    }
}
