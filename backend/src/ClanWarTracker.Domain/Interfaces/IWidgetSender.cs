namespace ClanWarTracker.Domain.Interfaces;

/// <summary>
/// Telegram-виджеты (Bot API 10.4): мини-HTML прямо в сообщении. В библиотеке Telegram.Bot
/// их ещё нет, поэтому отдельный клиент. Пока функция не включена в настройках или Telegram
/// её не принял, Enabled = false - и бот отвечает обычным текстом, как раньше.
/// </summary>
public interface IWidgetSender
{
    bool Enabled { get; }

    /// <summary>Новый общий виджет. null - не вышло (тогда Enabled на время гаснет).</summary>
    Task<string?> SendSharedNewAsync(long chatId, string html, string publicData, string caption,
        int? threadId = null, CancellationToken ct = default);

    /// <summary>Показать уже созданный общий виджет в чате. false - не вышло.</summary>
    Task<bool> SendSharedAsync(long chatId, string widgetId, string caption,
        int? threadId = null, CancellationToken ct = default);

    /// <summary>
    /// Новые данные общего виджета - сразу во всех его копиях и пересылках.
    /// cacheTimeSeconds - как часто клиенты перечитывают их (5-3600 с).
    /// </summary>
    Task<WidgetEditResult> EditSharedAsync(string widgetId, string publicData, int cacheTimeSeconds,
        CancellationToken ct = default);
}

public enum WidgetEditResult { Ok, NotModified, Gone, Failed }
