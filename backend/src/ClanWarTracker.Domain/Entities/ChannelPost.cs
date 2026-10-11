namespace ClanWarTracker.Domain.Entities;

public enum ChannelPostState
{
    /// <summary>Ждёт владельца в панели: новость из ленты или черновик «посмотреть, как выглядит».</summary>
    Draft = 0,
    Published = 1,
    Rejected = 2,
    /// <summary>Telegram отказал (бота сняли с админов и т.п.). Можно опубликовать заново.</summary>
    Failed = 3,
}

/// <summary>
/// Пост в канал бота: опубликованный или ждущий решения владельца.
///
/// Текст хранится в «простой разметке» (обычный текст, **жирный**), а не в HTML:
/// владелец правит черновик в панели, и HTML-теги в поле ввода он бы сломал первой же правкой.
/// </summary>
public class ChannelPost
{
    public int Id { get; set; }

    /// <summary>Откуда пост: manual, news, daily, weekly, chstart, chend, card.</summary>
    public required string Kind { get; set; }

    public ChannelPostState State { get; set; }

    /// <summary>
    /// Ключ источника для дедупа: ссылка новости, «card:26000000». Уникален - одна и
    /// та же новость из ленты не становится вторым черновиком. null - ручной пост.
    /// </summary>
    public string? SourceKey { get; set; }

    /// <summary>Заголовок для списка в панели (в сам пост не идёт отдельно).</summary>
    public string? Title { get; set; }

    public required string Text { get; set; }

    /// <summary>Картинка над текстом - публичный адрес, Telegram качает её сам.</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>Первоисточник новости - для владельца в панели.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Кнопка под постом. Url вида «startapp:duel» - в нужный раздел приложения.</summary>
    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? PublishedUtc { get; set; }

    /// <summary>Номер сообщения в канале - для ссылки «открыть пост».</summary>
    public int? MessageId { get; set; }

    public string? Error { get; set; }
}
