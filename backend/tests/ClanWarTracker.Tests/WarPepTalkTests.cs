using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Tests;

/// <summary>Напутствие к напоминанию: цифры для нейросети и чистка её ответа перед чатом.</summary>
public class WarPepTalkTests
{
    private static WarParticipant P(string tag, int today) => new() { PlayerTag = tag, Name = tag, DecksUsedToday = today };

    private static WarStatus War() => new()
    {
        ClanTag = "#US", PeriodType = "warDay", PeriodIndex = 4, DayEndsAtUtc = DateTime.UtcNow.AddHours(3),
        Participants = [P("#A", 4), P("#B", 4), P("#C", 2), P("#D", 0), P("#GONE", 0)],
        RaceClans =
        [
            new RaceClanStanding { Tag = "#R1", Name = "R1", Fame = 9000 },
            new RaceClanStanding { Tag = "#US", Name = "Us", Fame = 8600, TodayFame = 2000, DecksUsedToday = 10 },
            new RaceClanStanding { Tag = "#R2", Name = "R2", Fame = 8300 },
        ],
    };

    [Fact]
    public void Facts_CountOnlyCurrentRoster_AndRaceGaps()
    {
        var roster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "#A", "#B", "#C", "#D" };

        var f = WarPepTalkUseCase.Facts.From(War(), roster, TimeSpan.FromMinutes(185));

        Assert.Equal(2, f.Played);
        Assert.Equal(2, f.Slackers);          // ушедший #GONE не считается
        Assert.Equal(6, f.DecksMissing);       // 2 + 4
        Assert.Equal(1200, f.MedalsAtStake);   // 6 колод по 200 медалей сегодняшнего темпа
        Assert.Equal((3, 5), (f.Hours, f.Minutes));
        Assert.Equal(2, f.Place);
        Assert.Equal((400, 300), (f.GapAhead, f.GapBehind));
    }

    [Fact]
    public void Clean_RemovesMentionsAndMarkdown_AndEscapesHtml()
    {
        var text = "**😤 Ребята,** @slacker 28 человек уже отыграли <за себя> и за клан. А вы — нет.";

        var clean = WarPepTalkUseCase.Clean(text);

        Assert.NotNull(clean);
        Assert.DoesNotContain("@", clean);
        Assert.DoesNotContain("**", clean);
        Assert.Contains("&lt;за себя&gt;", clean);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("коротко")]
    public void Clean_RejectsEmptyOrTooShort(string? text) => Assert.Null(WarPepTalkUseCase.Clean(text));

    [Fact]
    public void Prompt_ForbidsNamesAndInsults_InEveryTone()
    {
        var f = new WarPepTalkUseCase.Facts(20, 5, 12, 2000, 2, 30, 2, 3, 5, 500, 200);
        foreach (var tone in new[] { "calm", "push", "hard" })
        {
            var p = WarPepTalkUseCase.Prompt("Клан", "ru", tone, PepMoment.Reminder, f, "угол");
            Assert.Contains("никого не называй", p);
            Assert.Contains("без мата, оскорблений", p);
            Assert.Contains("12", p);
        }
    }
}
