using System.Text;
using System.Text.Json;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Аналитик меты: раз в день нейросеть просматривает статистику колод топа за неделю и
/// отмечает лучшие - с названием архетипа и одной фразой, чем колода хороша.
///
/// Выбирать она может только из пронумерованного списка реально сыгранных колод: номер,
/// которого в списке нет, просто отбрасывается. Поэтому придумать колоду или процент
/// побед модель не может - она только расставляет акценты в настоящих данных.
/// Разбор показывается в билдере колод; игрокам нейросеть ничего напрямую не пишет.
/// </summary>
public class MetaAnalystUseCase(
    INewsTranslator ai,
    IMetaRepository meta,
    IClashRoyaleApi crApi,
    GetTopMetaUseCase topMeta,
    IServiceSettingRepository settings)
{
    public const string SettingKey = "meta.analyst";

    /// <summary>Сколько колод показываем модели: больше - дороже и не лучше, меньше - выбирать не из чего.</summary>
    private const int Shown = 40;
    private const int MinGames = 30;
    private const int MaxPicks = 8;

    /// <summary>Не раньше этого часа по Киеву: ночной снимок меты к этому времени уже собран.</summary>
    private const int RunHourLocal = 7;
    private const int TzOffsetMinutes = 180;

    public record Pick(string Key, string Tag, Dictionary<string, string> Note);

    public record Report(string Day, Dictionary<string, string> Summary, List<Pick> Picks, DateTime AtUtc);

    /// <summary>Такт воркера: разбор раз в сутки, после утреннего часа. true - разбор обновлён.</summary>
    public async Task<bool> TickAsync(CancellationToken ct = default)
    {
        if (!ai.Enabled) return false;
        var local = DateTime.UtcNow.AddMinutes(TzOffsetMinutes);
        if (local.Hour < RunHourLocal) return false;
        var day = local.ToString("yyyy-MM-dd");
        var last = await LastAsync(ct);
        if (last?.Day == day) return false;
        // Неудачный разбор (кривой JSON, пустой выбор) иначе повторялся бы каждые пять
        // минут до конца дня - платным запросом к нейросети.
        if (!await DailyAttempts.TryStartAsync(settings, AttemptsKey, day, 3, ct)) return false;
        return await RunAsync(ct) is null;
    }

    private const string AttemptsKey = "meta.analyst.attempts";

    public async Task<Report?> LastAsync(CancellationToken ct = default)
    {
        var raw = await settings.GetAsync(SettingKey, ct);
        if (string.IsNullOrEmpty(raw)) return null;
        try { return JsonSerializer.Deserialize<Report>(raw); }
        catch (JsonException) { return null; }
    }

    /// <summary>Разобрать мету сейчас. null - готово, иначе причина, почему нет.</summary>
    public async Task<string?> RunAsync(CancellationToken ct = default)
    {
        if (!ai.Enabled) return "Нейросеть не подключена";
        var window = await MetaWindow.LoadAsync(meta, ct);
        if (window is null) return "Меты ещё нет - бот не собрал бои топа";

        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var decks = window.Decks
            .Where(d => d.Games >= MinGames)
            .OrderByDescending(d => MetaAggregator.WilsonLower(d.Wins, d.Games))
            .Take(Shown)
            .ToList();
        if (decks.Count < 5) return "Слишком мало колод с достаточным числом игр";

        var sb = new StringBuilder();
        sb.AppendLine($"Колоды топа за {window.FromDayUtc} - {window.ToDayUtc} (боёв в выборке: {window.Battles}):");
        for (var i = 0; i < decks.Count; i++)
        {
            var d = decks[i];
            var names = MetaCard.ParseDeckKey(d.Key).Select(k =>
                (catalog.GetValueOrDefault(Math.Abs(k))?.Name ?? $"#{Math.Abs(k)}") + (k < 0 ? " (evo)" : ""));
            sb.AppendLine($"{i + 1}. {string.Join(", ", names)} | игр {d.Games}, побед {Math.Round(100.0 * d.Wins / d.Games, 1)}%");
        }
        try
        {
            var m = await topMeta.ExecuteAsync(ct);
            if (m is not null && m.Cards.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Популярность карт в колодах топ-1000 и изменение за неделю: " + string.Join(", ",
                    m.Cards.Take(20).Select(c => $"{c.Name} {c.Percent}%{(c.DeltaPercent is double x ? $" ({(x >= 0 ? "+" : "")}{x})" : "")}")));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* без трендов карт разбор всё равно возможен */ }

        var prompt = $$"""
            Ты аналитик меты Clash Royale. Ниже настоящая статистика колод топ-игроков за неделю.
            Выбери до {{MaxPicks}} колод, которые стоит играть обычному игроку сейчас: высокий процент побед при достаточном
            числе игр, колоды на подъёме по трендам карт; разные архетипы, а не восемь вариантов одной колоды.
            Выбирать можно ТОЛЬКО из списка - по номеру. Ничего не выдумывай: ни колод, ни цифр.

            Для каждой выбранной колоды:
            - tag - короткое общепринятое название архетипа латиницей (например: LavaLoon, Hog 2.6, Golem Beatdown, X-Bow, Log Bait);
            - note - чем хороша, одной фразой до 90 символов, на трёх языках (ru, uk, en).
            И summary - 1-2 предложения о мете недели на трёх языках.

            Ответ строго JSON без пояснений:
            {"summary": {"ru": "...", "uk": "...", "en": "..."},
             "picks": [{"n": 1, "tag": "...", "note": {"ru": "...", "uk": "...", "en": "..."} }] }

            {{sb}}
            """;

        var raw = await ai.ChatJsonAsync(prompt, 2000, ct);
        if (string.IsNullOrWhiteSpace(raw)) return ai.LastError ?? "Нейросеть не ответила";
        var report = Parse(raw, decks.Select(d => d.Key).ToList(), DateTime.UtcNow.AddMinutes(TzOffsetMinutes).ToString("yyyy-MM-dd"));
        if (report is null || report.Picks.Count == 0) return "Нейросеть ответила, но без пригодного выбора";
        await settings.SetAsync(SettingKey, JsonSerializer.Serialize(report), ct);
        return null;
    }

    /// <summary>Ответ модели → разбор. Номера вне списка и повторы отбрасываются.</summary>
    private static Report? Parse(string raw, List<string> keys, string day)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var root = doc.RootElement;
            var picks = new List<Pick>();
            var seen = new HashSet<int>();
            if (root.TryGetProperty("picks", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in arr.EnumerateArray())
                {
                    if (!p.TryGetProperty("n", out var n) || !n.TryGetInt32(out var i)) continue;
                    if (i < 1 || i > keys.Count || !seen.Add(i)) continue;
                    var tag = p.TryGetProperty("tag", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
                    picks.Add(new Pick(keys[i - 1], Cut(tag.Trim(), 30), Langs(p, "note", 140)));
                    if (picks.Count >= MaxPicks) break;
                }
            }
            return new Report(day, Langs(root, "summary", 400), picks, DateTime.UtcNow);
        }
        catch (JsonException) { return null; }
    }

    private static Dictionary<string, string> Langs(JsonElement e, string name, int max)
    {
        var result = new Dictionary<string, string>();
        if (!e.TryGetProperty(name, out var v)) return result;
        if (v.ValueKind == JsonValueKind.String) { result["ru"] = Cut(v.GetString()!.Trim(), max); return result; }
        if (v.ValueKind != JsonValueKind.Object) return result;
        foreach (var lang in new[] { "ru", "uk", "en" })
            if (v.TryGetProperty(lang, out var s) && s.ValueKind == JsonValueKind.String)
                result[lang] = Cut(s.GetString()!.Replace("**", "").Trim(), max);
        return result;
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
