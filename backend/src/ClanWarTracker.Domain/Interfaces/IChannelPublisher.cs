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

    /// <summary>
    /// Меняет текст уже опубликованного поста. Картинка остаётся: пост-фото правится как
    /// подпись, длинный пост с большим превью - как текст с тем же превью. Кнопки
    /// передаются заново, иначе Telegram мог бы снять клавиатуру.
    /// </summary>
    Task<ChannelEditResult> EditAsync(long channelId, int messageId, string html, string? photoUrl,
        IReadOnlyList<IReadOnlyList<BotButton>> rows, CancellationToken ct = default);
}

public enum ChannelEditResult { Ok, TooLongForCaption, Failed }

/// <summary>
/// Запись из ленты: новость сайта, видео YouTube, пост Reddit или статья со страницы блога.
/// VideoId - у видео YouTube: такой пост оформляется анонсом «смотреть», а не пересказом.
/// Score - апвоуты поста Reddit: по ним бот сперва отсеивает неинтересное, бесплатно.
/// </summary>
public record NewsItem(string Key, string Title, string? Summary, string Link, string? ImageUrl, DateTime? PublishedUtc,
    string? VideoId = null, int? Score = null)
{
    public bool IsReddit => Score is not null;
}

/// <summary>Что удалось достать со страницы самой новости: полный текст и нормальная картинка.</summary>
public record NewsArticle(string? Title, string? Description, string? Text, string? ImageUrl);

public interface INewsFeedReader
{
    /// <summary>
    /// Записи ленты, свежие первыми. Понимает RSS/Atom, а обычную страницу блога читает как
    /// список статей по ссылкам на ней. Бросает исключение, если адрес не прочитать.
    /// </summary>
    Task<List<NewsItem>> ReadAsync(string feedUrl, CancellationToken ct = default);

    /// <summary>
    /// Полный текст и картинка новости с её страницы; у видео - обложка в лучшем качестве.
    /// null - страница не открылась или текста на ней нет.
    /// </summary>
    Task<NewsArticle?> ReadArticleAsync(NewsItem item, CancellationToken ct = default);
}

/// <summary>Новость целиком - то, из чего пишется пост.</summary>
/// <param name="RedditScore">Пост с Reddit и его апвоуты: модель решает, интересен ли он, строже, чем новость.</param>
public record NewsSource(string Title, string? Summary, string? Body, bool IsVideo, int? RedditScore = null);

/// <summary>
/// Пост из новости по-русски для канала. Работает, только если на сервере задан ключ
/// API; без него новость оформляется без пересказа, на языке оригинала.
/// </summary>
public interface INewsTranslator
{
    bool Enabled { get; }

    /// <summary>Кто пишет посты: «DeepSeek», «Claude» или пусто, если ключа нет.</summary>
    string Provider { get; }

    /// <summary>Почему не вышел последний запрос (неверный ключ, кончился баланс…). null - вышел.</summary>
    string? LastError { get; }

    /// <summary>Пробный короткий запрос: null - нейросеть отвечает, иначе причина по-русски.</summary>
    Task<string?> CheckAsync(CancellationToken ct = default);

    /// <returns>
    /// Заголовок для панели и готовый текст поста в простой разметке. Пустой заголовок -
    /// это не новость об игре (мем, вопрос, оффтоп). null - пересказать не вышло.
    /// </returns>
    Task<(string Title, string Text)?> RetellAsync(NewsSource news, CancellationToken ct = default);
}
