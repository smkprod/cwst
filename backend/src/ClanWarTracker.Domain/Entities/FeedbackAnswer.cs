namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Ответ игрока на короткий опрос: какие функции добавить и почему купил (или что мешает
/// купить Плюс). Строка на человека: ответил ещё раз - перезаписали, мнение важно свежее.
/// </summary>
public class FeedbackAnswer
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }
    public string? PlayerTag { get; set; }

    /// <summary>buyer - платил; player - нет. На момент ответа.</summary>
    public required string Segment { get; set; }

    /// <summary>Ключи желаемых функций через запятую - см. FeedbackUseCase.Wants.</summary>
    public required string Wants { get; set; }

    /// <summary>За что купил (buyer) или что мешает купить (player) - ключ.</summary>
    public string? Reason { get; set; }

    /// <summary>Своими словами. Пусто - не написал.</summary>
    public string? Text { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
