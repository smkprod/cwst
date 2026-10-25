using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Tests;

/// <summary>Ответ нейросети показываем как есть, поэтому чистим его от разметки и лишнего.</summary>
public class ClanAiTests
{
    [Fact]
    public void Clean_StripsMarkdown_AndEmptyLines()
    {
        var text = "## Сводка\n\n**🕐 Играют** днём\n\n`💪` Держатся на лидере\n";

        Assert.Equal("Сводка\n🕐 Играют днём\n💪 Держатся на лидере", ClanAiUseCase.Clean(text));
    }

    [Fact]
    public void Clean_CutsTooLongAnswer()
    {
        var text = new string('а', 2000);

        var clean = ClanAiUseCase.Clean(text);

        Assert.True(clean.Length <= 901);
        Assert.EndsWith("…", clean);
    }

    [Theory]
    [InlineData("uk", "українською мовою")]
    [InlineData("en", "in English")]
    [InlineData("xx", "по-русски")]
    public void Lang_FallsBackToRussian(string lang, string expected) =>
        Assert.Equal(expected, ClanAiUseCase.Lang(lang));

    [Fact]
    public void TrainerCovers_TheEnemyCardsOfThePuzzles() =>
        Assert.Equal(8, TrainerAiUseCase.Covered.Count);
}
