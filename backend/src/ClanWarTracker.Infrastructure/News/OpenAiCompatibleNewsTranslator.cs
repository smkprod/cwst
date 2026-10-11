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
public class OpenAiCompatibleNewsTranslator(HttpClient http, string provider, string baseUrl, string? apiKey, string model)
    : INewsTranslator
{
    public bool Enabled => !string.IsNullOrWhiteSpace(apiKey);
    public string Provider => Enabled ? provider : "";
    public string? LastError { get; private set; }

    public async Task<string?> CheckAsync(CancellationToken ct = default)
    {
        if (!Enabled) return "Ключ не задан";
        var answer = await AskAsync(NewsPrompts.PingPrompt, 30, ct);
        return answer is null ? LastError ?? "Нет ответа" : null;
    }

    public async Task<(string Title, string Text)?> RetellAsync(NewsSource news, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        return NewsPrompts.Parse(await AskAsync(NewsPrompts.Build(news), 1200, ct));
    }

    public Task<(string Title, string Text)?> ComposeAsync(string request, string data, CancellationToken ct = default) =>
        WriteAsync(NewsPrompts.Compose(request, data), ct);

    public Task<(string Title, string Text)?> RewriteAsync(string instruction, string post, string data,
        CancellationToken ct = default) =>
        WriteAsync(NewsPrompts.Rewrite(instruction, post, data), ct);

    private async Task<(string Title, string Text)?> WriteAsync(string prompt, CancellationToken ct)
    {
        if (!Enabled) return null;
        var post = NewsPrompts.Parse(await AskAsync(prompt, 1800, ct));
        if (post is { Text.Length: 0 })
        {
            LastError = "Нейросеть вернула пустой пост";
            return null;
        }
        return post;
    }

    /// <summary>Один запрос к /chat/completions. null - не вышло, причина в LastError.</summary>
    private async Task<string?> AskAsync(string prompt, int maxTokens, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = JsonContent.Create(new
            {
                model,
                max_tokens = maxTokens,
                temperature = 0.7,
                // Ответ строго JSON-объектом: в задании об этом сказано, а режим это гарантирует
                response_format = new { type = "json_object" },
                messages = new[] { new { role = "user", content = prompt } },
            });
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = NewsPrompts.Describe(resp.StatusCode);
                return null;
            }
            var body = await resp.Content.ReadFromJsonAsync<Response>(cancellationToken: ct);
            var text = body?.Choices?.FirstOrDefault()?.Message?.Content;
            LastError = string.IsNullOrWhiteSpace(text) ? "Нейросеть вернула пустой ответ" : null;
            return text;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LastError = ex is JsonException ? "Непонятный ответ нейросети" : NewsPrompts.NetworkError;
            return null;
        }
    }

    private record Response([property: JsonPropertyName("choices")] List<Choice>? Choices);
    private record Choice([property: JsonPropertyName("message")] Message? Message);
    private record Message([property: JsonPropertyName("content")] string? Content);
}
