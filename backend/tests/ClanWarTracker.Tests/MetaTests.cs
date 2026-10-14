using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using static ClanWarTracker.Tests.TestData;

namespace ClanWarTracker.Tests;

/// <summary>Мета топа: из неё строятся витрина колод, конструктор и посты канала.</summary>
public class MetaTests
{
    [Fact]
    public void SameBattleFromBothLogs_CountedOnce()
    {
        // Бой двух топ-игроков приходит из журнала каждого - с зеркальными сторонами
        var fromA = Recent(0, "#A", "#B", won: true, DeckA, DeckB);
        var fromB = Recent(0, "#B", "#A", won: false, DeckB, DeckA, crownsFor: 0, crownsAgainst: 1);

        var r = MetaAggregator.Build([fromA, fromB]);

        Assert.Equal(1, r.Battles);
        Assert.Equal(1, r.Decks[MetaCard.DeckKey(DeckA)].Games);
        Assert.Equal(1, r.Decks[MetaCard.DeckKey(DeckA)].Wins);
        Assert.Equal(0, r.Decks[MetaCard.DeckKey(DeckB)].Wins);
    }

    [Fact]
    public void NonRankedAndTwoVsTwoAndIncompleteDecks_Skipped()
    {
        var friendly = Recent(0, "#A", "#B", true, DeckA, DeckB, type: "friendly");
        var twoVsTwo = Recent(1, "#A", "#B", true, DeckA, DeckB);
        twoVsTwo.TeamTags.Add("#C");
        var shortDeck = Recent(2, "#A", "#B", true, [1, 2, 3], DeckB);

        var r = MetaAggregator.Build([friendly, twoVsTwo, shortDeck]);

        Assert.Equal(0, r.Battles);
        Assert.Empty(r.Decks);
    }

    [Fact]
    public void Draw_IsNeitherWinNorLoss()
    {
        var draw = Recent(0, "#A", "#B", won: false, DeckA, DeckB, crownsFor: 1, crownsAgainst: 1);

        var r = MetaAggregator.Build([draw]);

        var a = r.Decks[MetaCard.DeckKey(DeckA)];
        Assert.Equal((1, 0, 1), (a.Games, a.Wins, a.Draws));
        Assert.Equal(0, r.Games![0].Result);
    }

    [Fact]
    public void Evolution_IsSeparateCardInDeckKey()
    {
        Assert.Equal(-26000000, MetaCard.Key(new CrDeckCard { Id = 26000000, Name = "x", EvolutionLevel = 1 }));
        Assert.Equal("1,2,3", MetaCard.DeckKey([3, 1, 2]));
    }

    [Fact]
    public void Wilson_SmallSampleRanksBelowLargeOne()
    {
        // 3 из 3 не должны обгонять 580 из 1000
        Assert.True(MetaAggregator.WilsonLower(3, 3) < MetaAggregator.WilsonLower(580, 1000));
        Assert.Equal(0, MetaAggregator.WilsonLower(0, 0));
    }

    [Fact]
    public void Synergy_CoPlayIsShareOfDecksWithBothCards()
    {
        var window = new MetaWindow.Data("2026-10-01", "synergy-test", new List<(string, int, int, int)>
        {
            ("1,2,3,4,5,6,7,8", 30, 15, 0),
            ("1,2,9,10,11,12,13,14", 10, 5, 0),
            // Эволюция и обычная версия - одна карта для связей
            ("-1,20,21,22,23,24,25,26", 10, 5, 0),
        }, [], 0);

        var syn = MetaSynergy.For(window);

        Assert.Equal(50, syn.Cards[1].Games);
        Assert.Equal(0.8, syn.CoPlay(1, 2), 3);   // 40 из 50
        Assert.Equal(0, syn.CoPlay(1, 1));
        Assert.Equal(1.0, syn.CoPlay(3, 1), 3);    // где тройка - там и единица

        var top = MetaSynergy.Top(syn, cards: 1, partners: 2);
        Assert.Equal(1, top[0].Id);
        Assert.Equal(2, top[0].Partners[0].Id);
        Assert.Equal(80, top[0].Partners[0].Percent);
    }
}
