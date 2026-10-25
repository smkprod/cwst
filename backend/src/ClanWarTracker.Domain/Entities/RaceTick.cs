namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Состояние одного клана гонки в получасовом окне военного дня - для «Разведки+».
///
/// Журнал войн CR отдаёт только итог дня, а вопрос главы другой: во сколько соперник
/// играет и когда к нему приходят очки. Ответ складывается из разниц между соседними
/// окнами, поэтому храним не итог, а ход дня. Строка на клан и полчаса: частый тик
/// воркера перезаписывает её, пока окно не закончилось.
/// </summary>
public class RaceTick
{
    public int Id { get; set; }
    public required string ClanTag { get; set; }
    public int SeasonId { get; set; }
    public int SectionIndex { get; set; }

    /// <summary>День недели гонки 3..6 - военные дни.</summary>
    public int DayIndex { get; set; }

    /// <summary>Начало получасового окна (UTC).</summary>
    public DateTime SlotUtc { get; set; }

    /// <summary>Медали клана за неделю на момент снимка (сумма участников).</summary>
    public int Fame { get; set; }

    /// <summary>Колоды, сыгранные кланом сегодня.</summary>
    public int DecksUsedToday { get; set; }

    /// <summary>Сколько человек сегодня сыграли хотя бы одну колоду.</summary>
    public int ActiveToday { get; set; }
}

/// <summary>
/// Один военный день одного игрока клана гонки: сколько колод, сколько медалей и когда
/// играл. Строка на игрока и день; пишем только тех, кто за неделю сыграл хоть раз.
/// </summary>
public class RacePlayerDay
{
    public int Id { get; set; }
    public required string ClanTag { get; set; }
    public required string PlayerTag { get; set; }
    public required string Name { get; set; }
    public int SeasonId { get; set; }
    public int SectionIndex { get; set; }
    public int DayIndex { get; set; }

    /// <summary>Колоды за этот день (последнее увиденное значение).</summary>
    public int Decks { get; set; }

    /// <summary>Медали игрока за неделю на конец дня (последнее увиденное значение).</summary>
    public int WeekFame { get; set; }

    /// <summary>Когда впервые за день увидели сыгранную колоду (с точностью до тика).</summary>
    public DateTime? FirstPlayUtc { get; set; }

    /// <summary>Когда в последний раз за день добавилась колода.</summary>
    public DateTime? LastPlayUtc { get; set; }

    /// <summary>Когда строку обновили - по нему чистим старое.</summary>
    public DateTime UpdatedUtc { get; set; }
}
