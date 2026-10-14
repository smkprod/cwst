using ClanWarTracker.Application.UseCases;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>Очки челленджа: от них зависит место в таблице и призы, ошибка тут видна всем.</summary>
public class ChallengeScoringTests
{
    [Fact]
    public void Tickets_EveryThirdWinInARowCountsDouble()
    {
        var battles = Enumerable.Range(0, 6).Select(i => Battle(i, 1)).ToList();

        var s = ChallengeUseCase.Compute(battles);

        // 1 + 1 + 2 + 1 + 1 + 2
        Assert.Equal(8, s.Tickets);
        Assert.Equal(6, s.Wins);
        Assert.Equal(6, s.BestStreak);
    }

    [Fact]
    public void Loss_BreaksStreakButKeepsTickets()
    {
        var s = ChallengeUseCase.Compute([Battle(0, 1), Battle(1, 1), Battle(2, -1), Battle(3, 1)]);

        Assert.Equal(3, s.Tickets);
        Assert.Equal(1, s.Losses);
        Assert.Equal(1, s.Streak);
        Assert.Equal(2, s.BestStreak);
    }

    [Theory]
    [InlineData("friendly")]
    [InlineData("riverRacePvP")]
    [InlineData("challenge")]
    [InlineData("clanMate")]
    public void OnlyLadderAndPathOfLegendsCount(string type)
    {
        var s = ChallengeUseCase.Compute([Battle(0, 1, type), Battle(1, 1, type)]);

        Assert.Equal(0, s.Tickets);
        Assert.Equal(0, s.Wins);
    }

    [Fact]
    public void PathOfLegends_Counts()
    {
        Assert.Equal(1, ChallengeUseCase.Compute([Battle(0, 1, "pathOfLegend")]).Tickets);
    }

    [Fact]
    public void ThreeCrowns_OnlyThreeCrownWinsScore()
    {
        var s = ChallengeUseCase.Compute(
            [Battle(0, 1, crownsFor: 3), Battle(1, 1, crownsFor: 2), Battle(2, 1, crownsFor: 3)],
            ChallengeRules.ThreeCrowns);

        Assert.Equal(2, s.Tickets);
        Assert.Equal(TestData.T0.AddMinutes(2), s.LastTicketUtc);
    }

    [Fact]
    public void Flawless_OnlyWinsWithoutLostTowers()
    {
        var s = ChallengeUseCase.Compute(
            [Battle(0, 1, crownsAgainst: 0), Battle(1, 1, crownsAgainst: 1)],
            ChallengeRules.Flawless);

        Assert.Equal(1, s.Tickets);
    }

    [Fact]
    public void Streak_ScoreIsBestStreak()
    {
        var s = ChallengeUseCase.Compute(
            [Battle(0, 1), Battle(1, 1), Battle(2, 1), Battle(3, -1), Battle(4, 1), Battle(5, 1)],
            ChallengeRules.Streak);

        Assert.Equal(3, s.Tickets);
        Assert.Equal(3, s.BestStreak);
    }

    [Fact]
    public void Heavy_UnknownDeckCostDoesNotScore()
    {
        double? Elixir(string key) => key == "heavy" ? 7.5 : key == "light" ? 3.0 : null;

        var s = ChallengeUseCase.Compute(
            [Battle(0, 1, deck: "heavy"), Battle(1, 1, deck: "light"), Battle(2, 1, deck: "unknown")],
            ChallengeRules.Heavy, Elixir);

        Assert.Equal(1, s.Tickets);
    }

    [Fact]
    public void UnknownRule_FallsBackToTickets()
    {
        Assert.Equal(ChallengeRules.Tickets, ChallengeRules.Normalize("whatever"));
        Assert.Equal(ChallengeRules.Tickets, ChallengeRules.Normalize(null));
    }
}
