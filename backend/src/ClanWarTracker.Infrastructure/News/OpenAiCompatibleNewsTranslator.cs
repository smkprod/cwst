using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.News;

/// <summary>
/// Пост из новости через API в формате OpenAI (/chat/completions). Так устроен DeepSeek -
/// включается одной строкой DEEPSEEK_API_KEY в .env сервера (модель по умолчанию
/// deepseek-chat, другая - DEEPSEEK_MODEL). Задание и разбор ответа - общие, NewsPrompts.
/// </summary>
public class OpenAiCompatibleNewsTranslator(HttpClient http, string baseUrl, string? apiKey, string model) : INewsTranslator
{
    public bool Enabled => !string.IsNullOrWhiteSpace(apiKey);

    public async Task<(string Title, string Text)?> RetellAsync(NewsSource news, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = JsonContent.Create(new
            {
                model,
                max_tokens = 1200,
                temperature = 0.7,
                // Ответ строго JSON-объектом: в задании об этом сказано, а режим это гарантирует
                response_format = new { type = "json_object" },
                messages = new[] { new { role = "user", content = NewsPrompts.Build(news) } },
            });
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadFromJsonAsync<Response>(cancellationToken: ct);
            return NewsPrompts.Parse(body?.Choices?.FirstOrDefault()?.Message?.Content);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private record Response([property: JsonPropertyName("choices")] List<Choice>? Choices);
    private record Choice([property: JsonPropertyName("message")] Message? Message);
    private record Message([property: JsonPropertyName("content")] string? Content);
}
