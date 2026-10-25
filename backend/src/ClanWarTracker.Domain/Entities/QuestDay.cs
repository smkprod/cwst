namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Задания дня одного человека за один его местный день: какие выпали, какие закрыты,
/// закрыт ли день целиком и какой серией.
///
/// Сами бои здесь не хранятся - задания на бои проверяются по журналу на каждый
/// запрос, как билеты челленджа. Храним только итог: отметка «сделано» не должна
/// пропасть, когда бой уйдёт из журнала, а серия - когда закончится день.
/// </summary>
public class QuestDay
{
    public int Id { get; set; }

    public long TelegramUserId { get; set; }

    public required string PlayerTag { get; set; }

    /// <summary>Клан на момент дня - для «в клане сегодня закрыли».</summary>
    public int? ClanId { get; set; }

    /// <summary>Местная дата человека, yyyy-MM-dd.</summary>
    public required string Day { get; set; }

    /// <summary>Ключи заданий через запятую, в порядке показа; бит в DoneMask - по позиции.</summary>
    public required string Quests { get; set; }

    public int DoneMask { get; set; }

    /// <summary>Когда закрыл все задания дня. null - ещё нет.</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>Серия на этот день: сколько дней подряд закрыто, включая этот. 0 - день не закрыт.</summary>
    public int Streak { get; set; }

    /// <summary>Сколько дней Плюса дали за этот день (за серию).</summary>
    public int RewardDays { get; set; }

    public DateTime CreatedUtc { get; set; }
}
