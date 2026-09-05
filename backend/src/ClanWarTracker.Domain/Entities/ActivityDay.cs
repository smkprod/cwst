namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Один активный день одного игрока.
///
/// Появилась потому, что посчитать «сколько людей заходило 20 августа» было неоткуда:
/// LastVisitAtUtc хранит только ПОСЛЕДНИЙ визит, и вчерашняя активность стирается
/// сегодняшней. Журнал нельзя восстановить задним числом — его можно только начать
/// вести, поэтому таблица предельно дешёвая: строка на человека в день, не на запрос.
/// </summary>
public class ActivityDay
{
    public int Id { get; set; }

    public int PlayerId { get; set; }

    /// <summary>Дата UTC в формате yyyy-MM-dd — как в респектах.</summary>
    public required string DayUtc { get; set; }

    /// <summary>
    /// Сколько за день было действий, меняющих что-то: пинок, респект, ответ в игре,
    /// смена настроек. Открытие приложения сюда не считается — сам факт строки уже
    /// означает, что человек заходил.
    /// </summary>
    public int Actions { get; set; }

    public DateTime FirstSeenUtc { get; set; }
}
