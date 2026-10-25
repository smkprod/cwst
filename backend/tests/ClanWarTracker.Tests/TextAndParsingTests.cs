using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Tests;

/// <summary>Тексты от пользователей и ответы нейросети: то, что приходит снаружи, не должно ломать бота.</summary>
public class TextAndParsingTests
{
    [Fact]
    public void ChannelHtml_EscapesTagsAndKeepsLiteMarkup()
    {
        var html = ChannelText.ToHtml("**Новости** <script>alert(1)</script> & [сайт](https://example.com/a?b=1)");

        Assert.Equal("<b>Новости</b> &lt;script&gt;alert(1)&lt;/script&gt; &amp; <a href=\"https://example.com/a?b=1\">сайт</a>", html);
    }

    [Theory]
    [InlineData("[клик](javascript:alert(1))")]
    [InlineData("[клик](ftp://example.com)")]
    public void ChannelHtml_OnlyHttpLinksBecomeLinks(string lite)
    {
        Assert.DoesNotContain("<a ", ChannelText.ToHtml(lite));
    }

    [Theory]
    [InlineData("https://www.reddit.com/r/ClashRoyale/.rss", "https://www.reddit.com/r/ClashRoyale")]
    [InlineData("reddit.com/r/ClashRoyale/top/?t=day", "https://www.reddit.com/r/ClashRoyale")]
    [InlineData("https://old.reddit.com/r/RoyaleRecruit", "https://www.reddit.com/r/RoyaleRecruit")]
    [InlineData("  https://example.com/feed.xml ", "https://example.com/feed.xml")]
    public void Feeds_RedditNormalizedOthersKept(string input, string expected)
    {
        Assert.Equal(expected, ChannelUseCase.NormalizeFeed(input));
    }

    [Theory]
    [InlineData("abc", "#ABC")]
    [InlineData(" #2pyo ", "#2PY0")]   // O и 0 в тегах игры - одно и то же
    public void PlayerTag_Normalized(string input, string expected)
    {
        Assert.Equal(expected, LinkPlayerUseCase.Normalize(input));
    }

    private static readonly List<string> Keys = ["d1", "d2", "d3"];

    [Fact]
    public void AnalystParse_TakesJsonFromChattyAnswer()
    {
        var raw = """
            Вот разбор:
            {"summary": {"ru": "Мета **быстрая**", "en": "Fast meta"},
             "picks": [{"n": 2, "tag": "цикл", "note": {"ru": "Быстрая"}}, {"n": 1, "tag": "танк", "note": "Медленная"}]}
            Удачи!
            """;

        var r = MetaAnalystUseCase.Parse(raw, Keys, "2026-10-14");

        Assert.NotNull(r);
        Assert.Equal("Мета быстрая", r!.Summary["ru"]);
        Assert.Equal(["d2", "d1"], r.Picks.Select(p => p.Key));
        Assert.Equal("Медленная", r.Picks[1].Note["ru"]);
    }

    [Fact]
    public void AnalystParse_DropsOutOfRangeAndDuplicatePicks()
    {
        // Всё, что нейросеть может прислать криво: вне списка, повтор, строка, null, не объект
        var raw = """{"picks": [{"n": 0}, {"n": 4}, {"n": 3}, {"n": 3}, {"n": "x"}, {"n": null}, "строка", 5, {"tag": "no n"}]}""";

        var r = MetaAnalystUseCase.Parse(raw, Keys, "d");

        Assert.Equal(["d3"], r!.Picks.Select(p => p.Key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("нет json")]
    [InlineData("{ broken json")]
    [InlineData("} {")]
    public void AnalystParse_GarbageIsNullNotCrash(string raw)
    {
        Assert.Null(MetaAnalystUseCase.Parse(raw, Keys, "d"));
    }
}

/// <summary>Нижнее меню: какие вкладки владелец может спрятать.</summary>
public class MenuTabsTests
{
    [Fact]
    public void ParseHidden_KeepsOnlyOptionalTabs()
    {
        Assert.Empty(ClanWarTracker.Domain.Entities.AppTabs.ParseHidden(null));
        Assert.Empty(ClanWarTracker.Domain.Entities.AppTabs.ParseHidden("  "));
        // Война и «Ещё» не прячутся, мусор и повторы отбрасываются
        Assert.Equal(["rating", "shop"],
            ClanWarTracker.Domain.Entities.AppTabs.ParseHidden("rating, war,shop,more,rating,xyz"));
    }
}
