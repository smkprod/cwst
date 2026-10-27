using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Tests;

/// <summary>«Военный штаб»: кто готов к КВ, кто может собрать колоды сильнее, кто не готов.</summary>
public class WarPrepTests
{
    [Fact]
    public void Classify_NoFourDecks_IsWeak()
    {
        Assert.Equal((WarPrepUseCase.Weak, "no_cards"),
            WarPrepUseCase.Classify(complete: false, best: 13, played: 12, games: 10, wins: 6, playsBest: false, clanMedian: 13));
    }

    [Fact]
    public void Classify_LevelsFarBelowClan_IsWeak()
    {
        Assert.Equal((WarPrepUseCase.Weak, "low_levels"),
            WarPrepUseCase.Classify(true, best: 11.5, played: 11.4, games: 10, wins: 6, playsBest: true, clanMedian: 13));
    }

    [Fact]
    public void Classify_PlaysWeakerThanCanBuild_IsBetter()
    {
        Assert.Equal((WarPrepUseCase.Better, "played_lower"),
            WarPrepUseCase.Classify(true, best: 13.4, played: 12.6, games: 10, wins: 6, playsBest: false, clanMedian: 13.2));
    }

    [Fact]
    public void Classify_Losing_OnlyWhenNotAlreadyOnBestDecks()
    {
        Assert.Equal((WarPrepUseCase.Better, "losing"),
            WarPrepUseCase.Classify(true, best: 13.2, played: 13.1, games: 10, wins: 3, playsBest: false, clanMedian: 13.2));
        // Уже играет тем, что подобрал бот, - лучше колодами не сделать
        Assert.Equal((WarPrepUseCase.Ready, null),
            WarPrepUseCase.Classify(true, best: 13.2, played: 13.1, games: 10, wins: 3, playsBest: true, clanMedian: 13.2));
        // Мало боёв - процент ничего не значит
        Assert.Equal(WarPrepUseCase.Ready,
            WarPrepUseCase.Classify(true, best: 13.2, played: 13.1, games: 4, wins: 0, playsBest: false, clanMedian: 13.2).Status);
    }

    [Fact]
    public void Classify_NoWarGames_ReadyWithNote()
    {
        Assert.Equal((WarPrepUseCase.Ready, "no_war_games"),
            WarPrepUseCase.Classify(true, best: 13.2, played: 0, games: 0, wins: 0, playsBest: false, clanMedian: 13.2));
    }

    [Fact]
    public void Median_HandlesOddEvenEmpty()
    {
        Assert.Equal(0, WarPrepUseCase.Median([]));
        Assert.Equal(12, WarPrepUseCase.Median([14, 11, 12]));
        Assert.Equal(12.5, WarPrepUseCase.Median([11, 14, 12, 13]));
    }

    [Fact]
    public void Post_ListsCountsAndWeakCards()
    {
        var s = new WarPrepUseCase.SummaryDto(45, 28, 9, 5, 3, 13.1,
            [new("Balloon", "", 4), new("Musketeer", "", 3)], [], DateTime.UtcNow);
        var text = WarPrepUseCase.Post(WarPrepText.Ru, "Волки", s);
        Assert.Contains("Волки", text);
        Assert.Contains("28 из 42", text);
        Assert.Contains("сильнее: 9", text);
        Assert.Contains("Balloon, Musketeer", text);
    }

    [Fact]
    public void WeekKey_IsStableWithinTheWeek()
    {
        var mon = new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(WarPrepUseCase.WeekKey(mon), WarPrepUseCase.WeekKey(mon.AddDays(3)));
        Assert.NotEqual(WarPrepUseCase.WeekKey(mon), WarPrepUseCase.WeekKey(mon.AddDays(7)));
    }
}

/// <summary>Подбор колод на КВ: уровень важнее процента побед в топе.</summary>
public class WarScoreTests
{
    [Fact]
    public void HigherLevelBeatsRareHighWinRateDeck()
    {
        // Своя колода 15.4 без статистики против колоды топа 12.2 с 85% побед на 400 играх
        Assert.True(DeckBuilderUseCase.WarScore(15.4, 0, 0) > DeckBuilderUseCase.WarScore(12.2, 340, 400));
        // Даже один уровень разницы не перебить процентом побед
        Assert.True(DeckBuilderUseCase.WarScore(14.0, 50, 100) > DeckBuilderUseCase.WarScore(13.0, 900, 1000));
    }

    [Fact]
    public void WinRateBreaksTiesAtEqualLevel()
    {
        Assert.True(DeckBuilderUseCase.WarScore(14.0, 600, 1000) > DeckBuilderUseCase.WarScore(14.0, 480, 1000));
        // Редкая колода с большим процентом почти не отличается от обычной: её процент тянем к 50%
        var rare = DeckBuilderUseCase.WarScore(14.0, 36, 40) - 14.0;
        var common = DeckBuilderUseCase.WarScore(14.0, 5500, 10000) - 14.0;
        Assert.True(rare < 0.5 + 1e-9 && common > 0.2);
    }
}
