namespace ClanWarTracker.Domain.Interfaces;

/// <param name="CanPost">Бот - администратор канала с правом публиковать.</param>
public record ChannelInfo(long Id, string Title, string? Username, bool CanPost);

/// <summary>Публикация в канал бота - отдельно от рассылок по чатам кланов.</summary>
public interface IChannelPublisher
{
    /// <summary>
    /// Канал по «@имя», ссылке t.me/имя или числовому id. null - канала нет или бот
    /// его не видит (в приватный канал бота сперва добавляют администратором).
    /// </summary>
    Task<ChannelInfo?> ResolveAsync(string handle, CancellationToken ct = default);

    /// <summary>
    /// Публикует пост: HTML-текст, картинка сверху (если есть) и кнопки. Возвращает
    /// номер сообщения или null, если Telegram отказал. Длинный текст с картинкой
    /// уходит одним сообщением с большим превью, а не обрезанной подписью.
    /// </summary>
    Task<int?> PublishAsync(long channelId, string html, string? photoUrl,
        IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct = default);
}

/// <summary>Запись из RSS/Atom-ленты: новость сайта, видео YouTube, пост Reddit.</summary>
public record NewsItem(string Key, string Title, string? Summary, string Link, string? ImageUrl, DateTime? PublishedUtc);

public interface INewsFeedReader
{
    /// <summary>Записи ленты, свежие первыми. Бросает исключение, если ленту не прочитать.</summary>
    Task<List<NewsItem>> ReadAsync(string feedUrl, CancellationToken ct = default);
}

/// <summary>
/// Пересказ англоязычной новости по-русски для канала. Работает, только если на
/// сервере задан ключ API; без него новости приходят черновиком на языке оригинала.
/// </summary>
public interface INewsTranslator
{
    bool Enabled { get; }

    /// <returns>Заголовок и текст поста в простой разметке (**жирный**). null - не вышло.</returns>
    Task<(string Title, string Text)?> RetellAsync(string title, string? summary, CancellationToken ct = default);
}
