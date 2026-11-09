using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using Xunit;

namespace ClanWarTracker.Tests;

/// <summary>
/// Живая карточка КВ: пределы Telegram для виджета (HTML до 32 КБ и до 4 КБ после zlib,
/// данные до 1024 байт) - сервер отказал бы на отправке, а заметили бы это только в бою.
/// </summary>
public class WarWidgetCardTests
{
    [Theory]
    [InlineData(BotLang.Ru)]
    [InlineData(BotLang.Uk)]
    [InlineData(BotLang.En)]
    public void HtmlFitsTelegramLimits(BotLang lang)
    {
        var html = WarWidgetCard.Html(lang, "clanify_bot");
        var bytes = Encoding.UTF8.GetBytes(html);
        Assert.True(bytes.Length <= 32_768, $"{bytes.Length} bytes of HTML");

        // Сервер жмёт zlib 9-го уровня; SmallestSize - тот же уровень. Держим запас до 4096,
        // как сам Telegram в своих примерах (3800)
        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.SmallestSize, leaveOpen: true)) z.Write(bytes);
        Assert.True(packed.Length <= 3800, $"{packed.Length} bytes after zlib");

        Assert.DoesNotContain("/*L*/", html);
        Assert.DoesNotContain("/*B*/", html);
        Assert.Contains("\"clanify_bot\"", html);
    }

    [Fact]
    public void LabelsCannotCloseTheScriptTag()
    {
        var html = WarWidgetCard.Html(BotLang.Ru, "x</script><b>");
        Assert.Equal(1, CountOf(html, "</script>"));
    }

    [Fact]
    public void DataStaysWithin1024BytesForAFullClanOfLongCyrillicNames()
    {
        var players = Enumerable.Range(1, 50)
            .Select(i => Player($"Очень длинное имя игрока {i}", i % 4))
            .ToList();
        var json = WarWidgetCard.Data(Status("warDay", players, periodIndex: 4), revision: 12);

        Assert.True(Encoding.UTF8.GetByteCount(json) <= WarWidgetCard.MaxDataBytes, $"{Encoding.UTF8.GetByteCount(json)} bytes");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(12, root.GetProperty("r").GetInt64());
        Assert.Equal(2, root.GetProperty("d").GetInt32());
        Assert.Equal(50, root.GetProperty("xn").GetInt32()); // всего недоигравших - даже если имён влезло меньше
        var shown = root.GetProperty("x").GetArrayLength();
        Assert.InRange(shown, 1, WarWidgetCard.MaxNames);
        // Первыми - те, кто ещё не начинал
        Assert.Equal(0, root.GetProperty("x")[0][1].GetInt32());
        // Имя обрезано, а не выброшено целиком
        Assert.EndsWith("…", root.GetProperty("x")[0][0].GetString());
    }

    [Fact]
    public void AtMostEightNamesSoTheButtonStaysInsideTheBubble()
    {
        var players = Enumerable.Range(1, 40).Select(i => Player("P" + i, 0)).ToList();
        using var doc = JsonDocument.Parse(WarWidgetCard.Data(Status("warDay", players), 1));
        Assert.Equal(WarWidgetCard.MaxNames, doc.RootElement.GetProperty("x").GetArrayLength());
        Assert.Equal(40, doc.RootElement.GetProperty("xn").GetInt32());
    }

    [Fact]
    public void DataCountsDoneAndListsOnlyThoseNotDone()
    {
        var players = new List<PlayerStatusDto> { Player("Аня", 4), Player("Боря", 2), Player("Вова", 0), Player("Гена", 4) };
        using var doc = JsonDocument.Parse(WarWidgetCard.Data(Status("warDay", players), 1));
        var root = doc.RootElement;

        Assert.Equal("w", root.GetProperty("k").GetString());
        Assert.Equal(2, root.GetProperty("p").GetInt32());
        Assert.Equal(4, root.GetProperty("n").GetInt32());
        Assert.Equal(2, root.GetProperty("xn").GetInt32());
        Assert.Equal("Вова", root.GetProperty("x")[0][0].GetString());
        Assert.Equal("Боря", root.GetProperty("x")[1][0].GetString());
        Assert.Equal(2, root.GetProperty("o").GetInt32());
        Assert.Equal(5, root.GetProperty("oc").GetInt32());
    }

    [Fact]
    public void TrainingDaysListNobody()
    {
        var players = new List<PlayerStatusDto> { Player("Аня", 0), Player("Боря", 1) };
        using var doc = JsonDocument.Parse(WarWidgetCard.Data(Status("training", players), 1));
        Assert.Equal("t", doc.RootElement.GetProperty("k").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("xn").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("d", out _));
    }

    [Fact]
    public void SameStatusGivesSameDataSoUnchangedCardsAreNotResent()
    {
        var players = new List<PlayerStatusDto> { Player("Аня", 1) };
        Assert.Equal(WarWidgetCard.Data(Status("warDay", players), 0), WarWidgetCard.Data(Status("warDay", players), 0));
    }

    [Fact]
    public void LanguageOfBotText()
    {
        Assert.Equal(BotLang.Uk, ClanWarTracker.Application.UseCases.WarWidgetUseCase.LangOf(BotText.Uk));
        Assert.Equal(BotLang.En, ClanWarTracker.Application.UseCases.WarWidgetUseCase.LangOf(BotText.En));
        Assert.Equal(BotLang.Ru, ClanWarTracker.Application.UseCases.WarWidgetUseCase.LangOf(BotText.Ru));
    }

    private static int CountOf(string s, string what)
    {
        var n = 0;
        for (var i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + 1, StringComparison.Ordinal)) n++;
        return n;
    }

    private static PlayerStatusDto Player(string name, int decksToday) => new(
        "#" + name.GetHashCode().ToString("X"), name, decksToday, decksToday, decksToday, 0, 0, 0, 0, 0, 0, 0,
        decksToday >= 4 ? "played" : "timeLeft", false, 0, null, 0, null, 0);

    private static ClanStatusDto Status(string period, List<PlayerStatusDto> players, int periodIndex = 3) => new(
        "#CLAN", "Клан", period, periodIndex, new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc), 5,
        new ClanStatsDto(0, 0, players.Sum(p => p.DecksUsedToday), 0, players.Count * 4, 0,
            players.Count(p => p.DecksUsedToday >= 4), 0, 0),
        null,
        Enumerable.Range(1, 5).Select(i => new RaceClanDto("#R" + i, "R" + i, i, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, i == 2, false)).ToList(),
        players, null, [], []);
}
