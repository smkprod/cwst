using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// Общее для всех моделей, которые пишут посты из новостей: задание и разбор ответа.
/// Провайдер (Claude, DeepSeek) отличается только тем, как отправить запрос.
/// </summary>
public static class NewsPrompts
{
    public const string ArticlePrompt = """
        Ты редактор русскоязычного Telegram-канала бота Clanify про Clash Royale (кланы, клановые войны, турниры, дуэли).
        Сделай из новости ниже пост для канала.

        Формат поля text:
        1) Первая строка: одно эмодзи по теме и короткий цепляющий заголовок жирным, например «⚖️ **Баланс: Hog Rider ослаблен**».
           Эмодзи: ⚖️ баланс, 🆕 новая карта или эволюция, 🎉 событие или испытание, 🛠 обновление, 🏆 турнир или киберспорт, 🎁 награды, 📢 прочее.
        2) Пустая строка, затем суть в 1-2 предложениях: что случилось и когда.
        3) Если в новости есть конкретика (изменения карт, награды, даты, правила события) - список пунктами «• », до 8 пунктов.
           Для баланса каждый пункт так: «⬆️ Карта - что изменилось (было → стало)» или «⬇️ …», для прочего «🔄 …».
        4) Если вывод для игрока очевиден - последней строкой «👉 » и одно предложение, что это значит или что сделать.

        Правила:
        - только факты из новости, ничего не выдумывай; нет цифр в тексте - не пиши цифр;
        - названия карт, режимов и событий оставляй по-английски, как в игре, если не уверен в официальном русском названии;
        - **жирный** только в заголовке и для 1-2 самых важных слов; без ссылок и хештегов;
        - весь пост не длиннее 900 символов;
        - если это не новость об игре (мем, вопрос игрока, реклама, оффтоп) - верни пустые title и text.

        Ответ строго JSON без пояснений: {"title": "заголовок без эмодзи и разметки", "text": "готовый пост"}
        """;

    public const string VideoPrompt = """
        Ты редактор русскоязычного Telegram-канала бота Clanify про Clash Royale.
        Вышло новое видео на официальном канале игры. Напиши короткий анонс.

        Формат поля text:
        1) Первая строка: «🎬 **короткий заголовок по-русски**».
        2) Пустая строка и 1-2 предложения: о чём видео и почему его стоит посмотреть (новая карта, обнова, событие).

        Правила: только то, что следует из названия и описания, ничего не выдумывай; названия карт и событий
        по-английски, как в игре; без ссылок и хештегов; до 350 символов.
        Если это не про игру - верни пустые title и text.

        Ответ строго JSON без пояснений: {"title": "заголовок без эмодзи и разметки", "text": "готовый пост"}
        """;

    /// <summary>Задание целиком: инструкция и сама новость.</summary>
    public static string Build(NewsSource news) => news.IsVideo
        ? $"{VideoPrompt}\n\nНазвание видео: {news.Title}\n\nОписание: {news.Summary}"
        : $"{ArticlePrompt}\n\nЗаголовок: {news.Title}\n\nАнонс: {news.Summary}\n\nТекст статьи:\n{news.Body ?? "(нет, есть только анонс)"}";

    /// <summary>
    /// Ответ модели → (заголовок, пост). Пустой пост - это «не новость об игре».
    /// null - ответ не разобрать.
    /// </summary>
    public static (string Title, string Text)? Parse(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;
        // Модель иногда оборачивает JSON в ```json … ``` - берём от первой { до последней }
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            var post = JsonSerializer.Deserialize<Post>(answer[start..(end + 1)]);
            if (post is null) return null;
            var title = post.Title?.Trim() ?? "";
            var text = post.Text?.Trim() ?? "";
            return text.Length == 0 ? ("", "") : (title.Length > 0 ? title : text.Split('\n')[0].Replace("**", "").Trim(), text);
        }
        catch (JsonException) { return null; }
    }

    private record Post([property: JsonPropertyName("title")] string? Title, [property: JsonPropertyName("text")] string? Text);
}
