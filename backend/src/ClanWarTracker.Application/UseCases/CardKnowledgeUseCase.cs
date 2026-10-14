using System.Text;
using System.Text.Json;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Знания о картах для конструктора колод, которые собирает нейросеть раз в день:
/// - роли карт, которых нет в таблице CardRoles (новые карты), - только из фиксированного
///   списка ролей, всё остальное отбрасывается;
/// - короткие подписи к самым сильным связям карт («Night Witch прикрывает Golem от роя»).
///   Сами связи и их цифры - из колод топа (MetaSynergy); модель только объясняет пару,
///   которую ей дали, и выбрать или придумать другую не может.
/// </summary>
public class CardKnowledgeUseCase(
    INewsTranslator ai,
    IMetaRepository meta,
    IClashRoyaleApi crApi,
    IServiceSettingRepository settings)
{
    private const string DayKey = "cards.knowledge.day";
    private const int RunHourLocal = 7;
    private const int TzOffsetMinutes = 180;

    /// <summary>Сколько сильнейших связей подписываем: этого хватает на любые колоды топа.</summary>
    private const int LinksShown = 60;
    private const int LinkMinGames = 60;

    public async Task<bool> TickAsync(CancellationToken ct = default)
    {
        if (!ai.Enabled) return false;
        var local = DateTime.UtcNow.AddMinutes(TzOffsetMinutes);
        if (local.Hour < RunHourLocal) return false;
        var day = local.ToString("yyyy-MM-dd");
        if (await settings.GetAsync(DayKey, ct) == day) return false;
        // Отметка дня - до запуска: упавший разбор иначе повторялся бы каждые пять минут.
        await settings.SetAsync(DayKey, day, ct);
        var error = await RunAsync(ct);
        return error is null;
    }

    /// <summary>Обновить знания сейчас. null - готово, иначе причина.</summary>
    public async Task<string?> RunAsync(CancellationToken ct = default)
    {
        if (!ai.Enabled) return "Нейросеть не подключена";
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        if (catalog.Count == 0) return "Справочник карт не открылся";

        var rolesError = await LearnRolesAsync(catalog, ct);
        var linksError = await NoteLinksAsync(catalog, ct);
        await settings.SetAsync(DayKey, DateTime.UtcNow.AddMinutes(TzOffsetMinutes).ToString("yyyy-MM-dd"), ct);
        return rolesError ?? linksError;
    }

    /* ---------------- Роли новых карт ---------------- */

    private async Task<string?> LearnRolesAsync(Dictionary<int, CrCatalogCard> catalog, CancellationToken ct)
    {
        Dictionary<string, string[]> learned;
        try
        {
            learned = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
                await settings.GetAsync(DeckBuilderUseCase.LearnedRolesKey, ct) ?? "{}") ?? [];
        }
        catch (JsonException) { learned = []; }

        var unknown = catalog.Values
            .Where(c => c.Id < 159000000 && !CardRoles.IsKnown(c.Name) && !learned.ContainsKey(c.Name))
            .Take(40)
            .ToList();
        if (unknown.Count == 0) return null;

        var list = string.Join("\n", unknown.Select(c => $"- {c.Name} ({c.ElixirCost} эликсира, {c.Rarity})"));
        var prompt = $$"""
            Ты эксперт Clash Royale. Для каждой карты из списка укажи её роли - ТОЛЬКО из этого набора:
            win (победная карта, бьёт башни), tank (большой танк), minitank (мини-танк), air (летает),
            antiair (бьёт воздух), splash (урон по площади), dps (высокий урон по одиночной цели),
            swarm (рой юнитов), building (здание), smallspell (дешёвое заклинание), bigspell (тяжёлое заклинание).
            Если не знаешь карту - пустой список, не угадывай.
            Ответ строго JSON: {"Название карты": ["role", ...], ...}

            {{list}}
            """;
        var raw = await ai.ChatJsonAsync(prompt, 1500, ct);
        if (string.IsNullOrWhiteSpace(raw)) return ai.LastError ?? "Нейросеть не ответила";
        try
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start < 0 || end <= start) return "Нейросеть ответила не JSON";
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var names = unknown.ToDictionary(c => c.Name, c => c.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!names.TryGetValue(p.Name, out var name) || p.Value.ValueKind != JsonValueKind.Array) continue;
                var roles = p.Value.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString()!.Trim().ToLowerInvariant())
                    .Where(r => CardRoles.All.Contains(r))
                    .Distinct()
                    .ToArray();
                // Пустые роли тоже запоминаем - чтобы не спрашивать про ту же карту каждый день
                learned[name] = roles;
            }
            await settings.SetAsync(DeckBuilderUseCase.LearnedRolesKey, JsonSerializer.Serialize(learned), ct);
            return null;
        }
        catch (JsonException) { return "Нейросеть ответила не JSON"; }
    }

    /* ---------------- Подписи к связям ---------------- */

    private async Task<string?> NoteLinksAsync(Dictionary<int, CrCatalogCard> catalog, CancellationToken ct)
    {
        var window = await MetaWindow.LoadAsync(meta, ct);
        if (window is null) return null;
        var syn = MetaSynergy.For(window);
        var top = syn.Pairs
            .Where(p => p.Value.Games >= LinkMinGames)
            .Select(p => (p.Key.A, p.Key.B, Co: Math.Max(syn.CoPlay(p.Key.A, p.Key.B), syn.CoPlay(p.Key.B, p.Key.A)),
                WinRate: (double)p.Value.Wins / p.Value.Games))
            .Where(p => catalog.ContainsKey(p.A) && catalog.ContainsKey(p.B))
            .OrderByDescending(p => p.Co)
            .Take(LinksShown)
            .ToList();
        if (top.Count == 0) return null;

        var sb = new StringBuilder();
        for (var i = 0; i < top.Count; i++)
            sb.AppendLine($"{i + 1}. {catalog[top[i].A].Name} + {catalog[top[i].B].Name}");
        var prompt = $$"""
            Ты эксперт Clash Royale. Ниже пары карт, которые топ-игроки чаще всего ставят в одну колоду.
            Для каждой пары одной фразой до 70 символов объясни, как карты помогают друг другу
            (например: «Night Witch добивает за Golem и прикрывает его от роя»). Только общеизвестная механика,
            без цифр и без выдумок. Если не уверен - пропусти пару.
            Ответ строго JSON: {"links": [{"n": 1, "note": {"ru": "...", "uk": "...", "en": "..."} }] }

            {{sb}}
            """;
        var raw = await ai.ChatJsonAsync(prompt, 3500, ct);
        if (string.IsNullOrWhiteSpace(raw)) return ai.LastError ?? "Нейросеть не ответила";
        try
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start < 0 || end <= start) return "Нейросеть ответила не JSON";
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var notes = new List<DeckBuilderUseCase.LinkNote>();
            if (doc.RootElement.TryGetProperty("links", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in arr.EnumerateArray())
                {
                    // Не-объект и не-число здесь бросили бы InvalidOperationException и уронили весь разбор
                    if (l.ValueKind != JsonValueKind.Object || !l.TryGetProperty("n", out var n)
                        || n.ValueKind != JsonValueKind.Number || !n.TryGetInt32(out var i) || i < 1 || i > top.Count) continue;
                    if (!l.TryGetProperty("note", out var note) || note.ValueKind != JsonValueKind.Object) continue;
                    var text = new Dictionary<string, string>();
                    foreach (var lang in new[] { "ru", "uk", "en" })
                        if (note.TryGetProperty(lang, out var s) && s.ValueKind == JsonValueKind.String)
                        {
                            var v = s.GetString()!.Replace("**", "").Trim();
                            text[lang] = v.Length <= 110 ? v : v[..110].TrimEnd() + "…";
                        }
                    if (text.Count > 0) notes.Add(new DeckBuilderUseCase.LinkNote(top[i - 1].A, top[i - 1].B, text));
                }
            }
            if (notes.Count > 0)
                await settings.SetAsync(DeckBuilderUseCase.LinkNotesKey, JsonSerializer.Serialize(notes), ct);
            return null;
        }
        catch (JsonException) { return "Нейросеть ответила не JSON"; }
    }
}
