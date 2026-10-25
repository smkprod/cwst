using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Tests;

/// <summary>Распорядок дня соперника в «Разведке+»: по нему глава решает, когда поднимать своих.</summary>
public class ScoutPlusTests
{
    private static readonly DateTime Start = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Полный день по получасам: колоды растут по заданной функции часа.</summary>
    private static List<RaceTick> Day(Func<double, int> decksAt, int dayIndex = 3, DateTime? start = null) =>
        Enumerable.Range(0, 48).Select(i =>
        {
            var h = i * 0.5;
            var d = decksAt(h);
            return new RaceTick
            {
                ClanTag = "#R", SeasonId = 1, SectionIndex = 0, DayIndex = dayIndex,
                SlotUtc = (start ?? Start).AddHours(h), DecksUsedToday = d, Fame = d * 200,
            };
        }).ToList();

    [Fact]
    public void EveningClan_PeaksInTheEvening_AndFinishesLate()
    {
        // Половина клана играет к 8-му часу дня, вторая половина - в 18..20
        var ticks = Day(h => h < 8 ? (int)(h * 10) : h < 18 ? 80 : h < 20 ? 80 + (int)((h - 18) * 60) : 200);

        var t = GetScoutPlusUseCase.Timing(ticks, Start, hoursNow: null, decksNow: null)!;

        Assert.Equal(1, t.DaysObserved);
        Assert.InRange(t.PeakHour, 18, 19);
        Assert.InRange(t.MostlyDoneHour!.Value, 19, 20);
        Assert.InRange(t.HourDecks.Sum(), 97, 103);
    }

    [Fact]
    public void TodayVsUsual_ComparesWithFinishedDays()
    {
        var past = Day(h => (int)Math.Min(200, h * 10));
        var t = GetScoutPlusUseCase.Timing(past, Start.AddDays(1), hoursNow: 10, decksNow: 150)!;

        Assert.Equal(50, t.UsualPercentNow);
        Assert.Equal(75, t.TodayPercentNow);
    }

    [Fact]
    public void PartialDay_CountsInHoursButNotInFinishTime()
    {
        // Купили Клан Про к обеду: утра нет, к какому часу доигрывают - судить рано
        var half = Day(h => (int)(h * 5)).Where(x => x.SlotUtc >= Start.AddHours(12)).ToList();

        var t = GetScoutPlusUseCase.Timing(half, Start, null, null)!;

        Assert.Null(t.MostlyDoneHour);
        Assert.True(t.HourDecks.Take(12).All(p => p == 0));
    }

    [Fact]
    public void NoTicks_NoTiming() =>
        Assert.Null(GetScoutPlusUseCase.Timing([], Start, null, null));

    [Fact]
    public void HoursIntoDay_WrapsAcrossDays() =>
        Assert.Equal(23, GetScoutPlusUseCase.HoursIntoDay(Start.AddHours(-1), Start), 3);
}

/// <summary>Штаб клана: в худшие попадают только реальные игроки, а не твинки и новички.</summary>
public class ClanHqTests
{
    private static Dictionary<string, GetClanHqUseCase.WeekRow> Week(params (string Tag, int Fame, int Decks)[] rows) =>
        rows.ToDictionary(r => r.Tag, r => new GetClanHqUseCase.WeekRow(r.Tag, r.Fame, r.Decks), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, ClanMemberInfo> Members(params string[] tags) =>
        tags.ToDictionary(t => t, t => new ClanMemberInfo(t, "member", 8000), StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Twink_AndNewcomer_AreExcluded_WeakRegularIsFlagged()
    {
        var weeks = new List<Dictionary<string, GetClanHqUseCase.WeekRow>>
        {
            Week(("#A", 3200, 16), ("#B", 3000, 16), ("#C", 1400, 12), ("#TWINK", 800, 8), ("#NEW", 900, 8)),
            Week(("#A", 3200, 16), ("#B", 3100, 16), ("#C", 1500, 12)),
            Week(("#A", 3100, 16), ("#B", 3000, 16), ("#C", 1600, 12)),
        };

        // Твинк ушёл из клана после войны, новичок в клане одну неделю
        var r = GetClanHqUseCase.Analyze(weeks, Members("#A", "#B", "#C", "#NEW"), []);

        Assert.Equal(["#C"], r.Attention.Select(a => a.Tag));
        Assert.Contains("low_fame", r.Attention[0].Reasons);
        Assert.Contains("skips", r.Attention[0].Reasons);
        Assert.Equal(1, r.Excluded);              // #NEW; твинка нет в составе - его не считаем вовсе
        Assert.Contains(r.Pillars, p => p.Tag == "#A");
    }

    [Fact]
    public void EvenClan_HasNobodyToFlag()
    {
        var w = Week(("#A", 3000, 16), ("#B", 3000, 16), ("#C", 2900, 16));
        var r = GetClanHqUseCase.Analyze([w, w], Members("#A", "#B", "#C"), []);

        Assert.Empty(r.Attention);
    }

    [Fact]
    public void LosingWarBattles_IsAReason()
    {
        var w = Week(("#A", 3000, 16), ("#B", 2900, 16), ("#C", 2900, 16));
        var battles = new Dictionary<string, GetClanHqUseCase.BattleRow>(StringComparer.OrdinalIgnoreCase)
        {
            ["#C"] = new(20, 6, 0),
        };
        var r = GetClanHqUseCase.Analyze([w, w], Members("#A", "#B", "#C"), battles);

        Assert.Equal("#C", r.Attention.Single().Tag);
        Assert.Equal(30, r.Attention[0].WinRate);
        Assert.Contains("losing", r.Attention[0].Reasons);
    }
}
