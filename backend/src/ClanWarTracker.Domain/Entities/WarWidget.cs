namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Живая карточка КВ клана в Telegram (общий виджет, Bot API 10.4): одна на клан и язык.
/// Каждый /status показывает тот же виджет заново, а воркер обновляет его данные, пока
/// карточку недавно открывали, - и в чате клана, и во всех её пересылках.
/// </summary>
public class WarWidget
{
    public int Id { get; set; }
    public int ClanId { get; set; }

    /// <summary>Язык подписей карточки: ru, uk, en. Код виджета неизменен, поэтому на язык - свой.</summary>
    public required string Lang { get; set; }

    /// <summary>id общего виджета, который вернул Telegram при первой отправке.</summary>
    public required string WidgetId { get; set; }

    /// <summary>Ревизия данных: карточка не откатится на старые цифры, пришедшие позже новых.</summary>
    public long Revision { get; set; }

    /// <summary>Последние отправленные данные без ревизии: одинаковые второй раз не шлём.</summary>
    public string? LastData { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Когда карточку последний раз показывали в чате: старые не обновляем.</summary>
    public DateTime LastShownUtc { get; set; }

    public DateTime? LastPushedUtc { get; set; }
}
