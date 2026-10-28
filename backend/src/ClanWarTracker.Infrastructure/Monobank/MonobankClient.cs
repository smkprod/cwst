using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Infrastructure.Monobank;

/// <summary>
/// Личный API Монобанка (api.monobank.ua): список банок и выписка по банке.
/// Ключ создаётся владельцем на api.monobank.ua и даёт только чтение.
/// </summary>
public class MonobankClient(HttpClient http, string? token) : IMonobankClient
{
    public bool Configured => !string.IsNullOrEmpty(token);

    public async Task<IReadOnlyList<MonoJar>> JarsAsync(CancellationToken ct = default)
    {
        var info = await SendAsync<ClientInfo>("personal/client-info", ct);
        return (info?.Jars ?? [])
            .Where(j => !string.IsNullOrEmpty(j.Id))
            .Select(j => new MonoJar(j.Id!, j.SendId ?? "", j.Title ?? ""))
            .ToList();
    }

    public async Task<IReadOnlyList<MonoTx>> StatementAsync(string accountId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var from = new DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var to = new DateTimeOffset(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var items = await SendAsync<List<StatementItem>>($"personal/statement/{Uri.EscapeDataString(accountId)}/{from}/{to}", ct);
        return (items ?? [])
            .Where(i => !string.IsNullOrEmpty(i.Id))
            .Select(i => new MonoTx(i.Id!, DateTimeOffset.FromUnixTimeSeconds(i.Time).UtcDateTime, i.Amount, i.Comment, i.Description))
            .ToList();
    }

    private async Task<T?> SendAsync<T>(string path, CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("MONOBANK_TOKEN is not set");
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Token", token);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            // 429 - чаще раза в минуту; тело с описанием ошибки - в сообщение, ключа там нет
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Monobank {(int)res.StatusCode}: {body[..Math.Min(body.Length, 200)]}");
        }
        return await res.Content.ReadFromJsonAsync<T>(ct);
    }

    private sealed class ClientInfo
    {
        [JsonPropertyName("jars")] public List<Jar>? Jars { get; set; }
    }

    private sealed class Jar
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("sendId")] public string? SendId { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
    }

    private sealed class StatementItem
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("time")] public long Time { get; set; }
        [JsonPropertyName("amount")] public long Amount { get; set; }
        [JsonPropertyName("comment")] public string? Comment { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }
}
