using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// Пересказ новости по-русски через Claude API. Включается двумя строками в .env
/// сервера: ANTHROPIC_API_KEY и ANTHROPIC_MODEL. Без них Enabled = false и новости
/// остаются черновиком на языке оригинала - владелец перепишет их сам.
/// </summary>
public class AnthropicNewsTranslator(HttpClient http, string? apiKey, string? model) : INewsTranslator
{
    public bool Enabled => !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(model);

    private const string Prompt = """
        Ты ведёшь русскоязычный Telegram-канал бота Clanify про Clash Royale (кланы, клановые войны, турниры).
        Перескажи новость ниже для канала. Правила:
        - пиши по-русски, живо и коротко: 2-5 предложений, без воды и без выдуманных фактов;
        - названия карт, режимов и событий оставляй как в игре по-английски, если не уверен в официальном русском названии;
        - можно 1-2 уместных эмодзи; **жирный** только для самого важного;
        - не добавляй ссылок и хештегов;
        - если это не новость об игре (мемы, вопросы игроков, оффтоп), верни пустой title.
        Ответ строго JSON без пояснений: {"title": "короткий заголовок", "text": "текст поста"}
        """;

    public async Task<(string Title, string Text)?> RetellAsync(string title, string? summary, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = JsonContent.Create(new
            {
                model,
                max_tokens = 700,
                messages = new[]
                {
                    new { role = "user", content = $"{Prompt}\n\nЗаголовок: {title}\n\nТекст: {summary}" },
                },
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
            return (post.Title?.Trim() ?? "", post.Text?.Trim() ?? "");
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
