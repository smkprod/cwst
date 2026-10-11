using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// Пост из новости по-русски через Claude API. Включается двумя строками в .env
/// сервера: ANTHROPIC_API_KEY и ANTHROPIC_MODEL. Без них Enabled = false и новость
/// оформляется без пересказа, на языке оригинала.
///
/// На вход - статья целиком, а не анонс из ленты: в анонсе «вышло обновление», а в
/// статье - какие карты апнули и насколько, а это и есть то, ради чего читают канал.
/// </summary>
public class AnthropicNewsTranslator(HttpClient http, string? apiKey, string? model) : INewsTranslator
{
    public bool Enabled => !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(model);

    private const string ArticlePrompt = """
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

    private const string VideoPrompt = """
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

    public async Task<(string Title, string Text)?> RetellAsync(NewsSource news, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            var content = news.IsVideo
                ? $"{VideoPrompt}\n\nНазвание видео: {news.Title}\n\nОписание: {news.Summary}"
                : $"{ArticlePrompt}\n\nЗаголовок: {news.Title}\n\nАнонс: {news.Summary}\n\nТекст статьи:\n{news.Body ?? "(нет, есть только анонс)"}";

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = JsonContent.Create(new
            {
                model,
                max_tokens = 1200,
                messages = new[] { new { role = "user", content } },
            });
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadFromJsonAsync<Response>(cancellationToken: ct);
            var text = body?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Модель иногда оборачивает JSON в ```json … ``` - берём от первой { до последней }
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            var post = JsonSerializer.Deserialize<Post>(text[start..(end + 1)]);
            if (post is null) return null;
            var title = post.Title?.Trim() ?? "";
            var postText = post.Text?.Trim() ?? "";
            return postText.Length == 0 ? ("", "") : (title, postText);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private record Response([property: JsonPropertyName("content")] List<Block>? Content);
    private record Block([property: JsonPropertyName("type")] string? Type, [property: JsonPropertyName("text")] string? Text);
    private record Post([property: JsonPropertyName("title")] string? Title, [property: JsonPropertyName("text")] string? Text);
}
