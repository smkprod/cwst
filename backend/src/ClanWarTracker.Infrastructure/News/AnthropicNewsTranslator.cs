using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// Пост из новости по-русски через Claude API. Включается двумя строками в .env
/// сервера: ANTHROPIC_API_KEY и ANTHROPIC_MODEL.
///
/// На вход - статья целиком, а не анонс из ленты: в анонсе «вышло обновление», а в
/// статье - какие карты апнули и насколько, а это и есть то, ради чего читают канал.
/// </summary>
public class AnthropicNewsTranslator(HttpClient http, string? apiKey, string? model) : INewsTranslator
{
    public bool Enabled => !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(model);

    public async Task<(string Title, string Text)?> RetellAsync(NewsSource news, CancellationToken ct = default)
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
                max_tokens = 1200,
                messages = new[] { new { role = "user", content = NewsPrompts.Build(news) } },
            });
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadFromJsonAsync<Response>(cancellationToken: ct);
            return NewsPrompts.Parse(body?.Content?.FirstOrDefault(c => c.Type == "text")?.Text);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private record Response([property: JsonPropertyName("content")] List<Block>? Content);
    private record Block([property: JsonPropertyName("type")] string? Type, [property: JsonPropertyName("text")] string? Text);
}
