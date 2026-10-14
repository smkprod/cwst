using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Infrastructure.ClashRoyale;

namespace ClanWarTracker.Tests;

/// <summary>
/// Номер сезона текущей недели: /currentriverrace его не отдаёт, вычисляем по журналу войн.
/// Ошибка тут размножает неделю в два сезона - история, рейтинг и Аллея славы врут.
/// </summary>
public class SeasonTests
{
    private static List<RiverRaceLogWeek> Log(params (int Season, int Section)[] weeks) =>
        weeks.Select(w => new RiverRaceLogWeek { SeasonId = w.Season, SectionIndex = w.Section }).ToList();

    [Theory]
    // Первая неделя нового сезона: последним в журнале - колизей прошлого
    [InlineData(0, 136, 3, 137)]
    // Обычная неделя внутри сезона
    [InlineData(1, 137, 0, 137)]
    [InlineData(3, 137, 2, 137)]
    // Смена недели: журнал уже записал неделю 0, а текущая война ещё отдаёт неделю 0.
    // Раньше здесь выходил 138 - и первая неделя 137 записывалась второй раз в «сезон 138»
    [InlineData(0, 137, 0, 137)]
    [InlineData(2, 137, 2, 137)]
    // Обратная гонка: текущая уже неделя 1, а журнал ещё не обновился - в нём колизей прошлого сезона
    [InlineData(1, 136, 3, 137)]
    // Журнал отстал на неделю внутри сезона - сезон тот же
    [InlineData(2, 137, 0, 137)]
    public void SeasonFromLog(int currentSection, int newestSeason, int newestSection, int expected)
    {
        var log = Log((newestSeason, newestSection), (newestSeason - 1, 1));

        Assert.Equal(expected, ClashRoyaleApiClient.RealSeasonId(currentSection, rawSeasonId: 0, log));
    }

    [Fact]
    public void EmptyLog_KeepsRawSeason()
    {
        Assert.Equal(42, ClashRoyaleApiClient.RealSeasonId(0, 42, []));
    }
}
