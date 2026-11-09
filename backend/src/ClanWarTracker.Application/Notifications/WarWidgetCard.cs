using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarTracker.Application.DTOs;

namespace ClanWarTracker.Application.Notifications;

/// <summary>
/// Живая карточка КВ клана - Telegram-виджет (Bot API 10.4): кто доиграл, колоды за день,
/// время до конца дня и место в гонке. Код карточки (war.html) неизменен после отправки,
/// меняются только данные - publicData, их пишет бот. Виджет «общий» (is_shared): одна
/// карточка на клан и язык, все её копии и пересылки показывают одни и те же живые цифры.
///
/// Пределы Telegram: HTML до 32 КБ и до 4 КБ после zlib, данные - до 1024 байт UTF-8.
/// Оба проверяют тесты (WarWidgetCardTests), иначе сервер отказал бы уже на отправке.
/// </summary>
public static class WarWidgetCard
{
    public const int MaxDataBytes = 1024;
    public const int MaxNameChars = 16;

    /// <summary>
    /// Больше имён карточка не покажет: виджет не выше 600 px и не прокручивается, и длинный
    /// список вытолкнул бы кнопку за край. Остальные - числом «и ещё N».
    /// </summary>
    public const int MaxNames = 8;

    private static readonly Lazy<string> Template = new(() =>
    {
        using var s = typeof(WarWidgetCard).Assembly.GetManifestResourceStream(
            "ClanWarTracker.Application.Notifications.Widgets.war.html")
            ?? throw new InvalidOperationException("war.html is not embedded");
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    });

    /// <summary>Подписи карточки. Ключи - те, что читает war.html.</summary>
    private static readonly Dictionary<BotLang, Dictionary<string, string>> Labels = new()
    {
        [BotLang.Ru] = new()
        {
            ["live"] = "live", ["wait"] = "Ждём данные войны", ["war"] = "Клановая война",
            ["day"] = "КВ · день {d} из 4", ["train"] = "Тренировочные дни", ["colo"] = "Колизей",
            ["trainBig"] = "Тренировка", ["done"] = "доиграли все 4 колоды",
            ["decks"] = "Колоды сегодня: {u} из {m}", ["left"] = "до конца дня",
            ["leftTrain"] = "до конца тренировки", ["place"] = "место в гонке",
            ["notDone"] = "Ещё не доиграли · {n}", ["all"] = "Все доиграли 🎉", ["more"] = "и ещё {n}",
            ["open"] = "Открыть в Clanify", ["join"] = "Подключить свой клан", ["h"] = "ч", ["m"] = "м",
        },
        [BotLang.Uk] = new()
        {
            ["live"] = "live", ["wait"] = "Чекаємо дані війни", ["war"] = "Кланова війна",
            ["day"] = "КВ · день {d} з 4", ["train"] = "Тренувальні дні", ["colo"] = "Колізей",
            ["trainBig"] = "Тренування", ["done"] = "дограли всі 4 колоди",
            ["decks"] = "Колоди сьогодні: {u} з {m}", ["left"] = "до кінця дня",
            ["leftTrain"] = "до кінця тренування", ["place"] = "місце в гонці",
            ["notDone"] = "Ще не дограли · {n}", ["all"] = "Усі дограли 🎉", ["more"] = "і ще {n}",
            ["open"] = "Відкрити в Clanify", ["join"] = "Підключити свій клан", ["h"] = "год", ["m"] = "хв",
        },
        [BotLang.En] = new()
        {
            ["live"] = "live", ["wait"] = "Waiting for war data", ["war"] = "Clan war",
            ["day"] = "War · day {d} of 4", ["train"] = "Training days", ["colo"] = "Colosseum",
            ["trainBig"] = "Training", ["done"] = "played all 4 decks",
            ["decks"] = "Decks today: {u} of {m}", ["left"] = "until day ends",
            ["leftTrain"] = "until training ends", ["place"] = "race position",
            ["notDone"] = "Not done yet · {n}", ["all"] = "Everyone is done 🎉", ["more"] = "and {n} more",
            ["open"] = "Open in Clanify", ["join"] = "Connect your clan", ["h"] = "h", ["m"] = "m",
        },
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        // Кириллица - как есть, а не \uXXXX: иначе имена съедят вдвое больше из 1024 байт
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>HTML карточки на языке клана, с юзернеймом бота для кнопки.</summary>
    public static string Html(BotLang lang, string botUsername)
    {
        // В скрипт - только JSON: строки экранированы, а «&lt;/script» из подписи не закроет тег
        var labels = JsonSerializer.Serialize(Labels[lang]).Replace("</", "<\\/");
        var bot = JsonSerializer.Serialize(botUsername.TrimStart('@'));
        return Template.Value
            .Replace("/*L*/{}", labels)
            .Replace("/*B*/\"\"", bot);
    }

    /// <summary>Подпись под карточкой: её видят и в списке чатов, и старые клиенты вместо карточки.</summary>
    public static string Caption(BotLang lang, string clanName) => lang switch
    {
        BotLang.Uk => $"⚔️ КВ клану «{clanName}»",
        BotLang.En => $"⚔️ {clanName}: clan war",
        _ => $"⚔️ КВ клана «{clanName}»",
    };

    /// <summary>
    /// Данные карточки (publicData) не длиннее 1024 байт. Имена недоигравших (до MaxNames) идут,
    /// пока влезают; сколько их всего - отдельным числом, и карточка допишет «и ещё N».
    /// </summary>
    public static string Data(ClanStatusDto s, long revision)
    {
        var kind = s.PeriodType switch { "warDay" => "w", "colosseum" => "c", _ => "t" };
        var ours = s.Race.FirstOrDefault(r => r.IsOurClan);
        var notDone = kind == "t"
            ? []
            : s.Players
                .Where(p => p.DecksUsedToday < 4)
                // Сначала те, кто ещё не начинал: их и надо пнуть первыми
                .OrderBy(p => p.DecksUsedToday)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        var card = new Card
        {
            R = revision,
            C = Cut(s.ClanName, 32),
            K = kind,
            D = kind == "w" && s.PeriodIndex >= 3 ? Math.Min(4, s.PeriodIndex - 2) : null,
            E = new DateTimeOffset(DateTime.SpecifyKind(s.DayEndsAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            U = s.Stats.TotalDecksUsedToday,
            M = s.Stats.MaxDecksToday,
            P = s.Players.Count(p => p.DecksUsedToday >= 4),
            N = s.Players.Count,
            O = ours?.Position > 0 ? ours.Position : null,
            Oc = s.Race.Count > 0 ? s.Race.Count : null,
            Xn = notDone.Count,
            X = [],
        };

        foreach (var p in notDone.Take(MaxNames))
        {
            card.X.Add([Cut(p.Name, MaxNameChars), p.DecksUsedToday]);
            if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(card, Json)) > MaxDataBytes)
            {
                card.X.RemoveAt(card.X.Count - 1);
                break;
            }
        }
        return JsonSerializer.Serialize(card, Json);
    }

    private static string Cut(string s, int max)
    {
        s = s.Trim();
        // По текстовым элементам, а не по char: эмодзи в нике не разрезать пополам
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(s);
        var sb = new StringBuilder();
        var n = 0;
        while (e.MoveNext())
        {
            if (++n > max) return sb.ToString().TrimEnd() + "…";
            sb.Append(e.GetTextElement());
        }
        return sb.ToString();
    }

    /// <summary>Короткие ключи - ради лимита в 1024 байта. Расшифровка - в war.html.</summary>
    private sealed class Card
    {
        [JsonPropertyName("r")] public long R { get; init; }
        [JsonPropertyName("c")] public string C { get; init; } = "";
        [JsonPropertyName("k")] public string K { get; init; } = "w";
        [JsonPropertyName("d")] public int? D { get; init; }
        [JsonPropertyName("e")] public long E { get; init; }
        [JsonPropertyName("u")] public int U { get; init; }
        [JsonPropertyName("m")] public int M { get; init; }
        [JsonPropertyName("p")] public int P { get; init; }
        [JsonPropertyName("n")] public int N { get; init; }
        [JsonPropertyName("o")] public int? O { get; init; }
        [JsonPropertyName("oc")] public int? Oc { get; init; }
        [JsonPropertyName("xn")] public int Xn { get; init; }
        [JsonPropertyName("x")] public List<object[]> X { get; init; } = [];
    }
}
