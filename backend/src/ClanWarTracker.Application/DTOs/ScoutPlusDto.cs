namespace ClanWarTracker.Application.DTOs;

/// <summary>
/// «Разведка+» (Клан Про) — разбор соперников изнутри: кто у них тащит войну, как эти
/// люди играют и во сколько клан обычно отыгрывает день.
///
/// Время везде в часах от начала военного дня (смены дня в CR), а не по часам: так
/// фронт сам переведёт его в пояс телефона, а сравнение дней не ломается переводом
/// часов. Начало текущего дня - <see cref="DayStartUtc"/>.
/// </summary>
public record ScoutPlusDto(
    bool IsWarDay,
    int DayIndex,                        // 3..6 - какой военный день идёт
    DateTime DayStartUtc,                // начало текущего военного дня
    double HoursIntoDay,                 // сколько часов дня уже прошло
    List<ScoutPlusClanDto> Clans);       // в порядке мест в гонке

public record ScoutPlusClanDto(
    string Tag,
    string Name,
    bool IsOurClan,
    int Fame,                            // медали за неделю
    int DecksToday,                      // колоды сегодня
    int ActiveToday,                     // сколько человек сегодня уже играли
    int Fighters,                        // сколько человек сыграло за неделю
    List<ScoutPlusPlayerDto> Top,        // лучшие по медалям за неделю
    ScoutTimingDto? Timing);             // null - ход дня ещё не набрался

public record ScoutPlusPlayerDto(
    string Tag,
    string Name,
    int Fame,
    int Decks,                           // военные колоды за неделю
    int FamePerDeck,                     // медалей за колоду: 200+ - почти без поражений
    int DaysPlayed,                      // дней с колодами из наблюдённых
    int DaysObserved,                    // сколько дней недели мы видели
    double? UsualHour);                  // во сколько обычно доигрывает, часов от начала дня

/// <summary>Как клан распределяет день: в какие часы играет и когда приходят очки.</summary>
public record ScoutTimingDto(
    int DaysObserved,                    // сколько военных дней легло в расчёт
    List<int> HourDecks,                 // 24 доли колод по часам дня, в процентах
    List<int> HourFame,                  // 24 доли медалей по часам дня, в процентах
    int PeakHour,                        // начало двухчасового окна с наибольшими медалями
    int PeakSharePercent,                // какая доля медалей дня приходит в это окно
    double? HalfDoneHour,                // к какому часу обычно сыграна половина колод
    double? MostlyDoneHour,              // к какому часу - 90%
    int? UsualPercentNow,                // какая доля дня обычно сыграна к этому часу
    int? TodayPercentNow);               // а сколько сыграно сегодня от их обычного дня

/// <summary>Как играет один игрок соперника: по его журналу боёв.</summary>
public record ScoutPlayerPlayDto(
    string Tag,
    string Name,
    int WarWins,
    int WarLosses,
    List<ScoutDeckDto> Decks);           // самые частые колоды в войне

public record ScoutDeckDto(int Games, int Wins, List<ScoutCardDto> Cards);

public record ScoutCardDto(string Name, string IconUrl, int Level, bool Evo);
