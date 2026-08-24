using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClanWarTracker.Application.Notifications;

/// <summary>Куда слать уведомление.</summary>
public enum NotifyChannel { Both = 0, Dm = 1, Chat = 2 }

public static class NotifyChannelExt
{
    public static bool WantsDm(this NotifyChannel c) => c is NotifyChannel.Both or NotifyChannel.Dm;
    public static bool WantsChat(this NotifyChannel c) => c is NotifyChannel.Both or NotifyChannel.Chat;

    public static string ToWire(this NotifyChannel c) => c switch
    {
        NotifyChannel.Dm => "dm",
        NotifyChannel.Chat => "chat",
        _ => "both",
    };

    public static NotifyChannel ParseChannel(string? wire) => wire?.ToLowerInvariant() switch
    {
        "dm" => NotifyChannel.Dm,
        "chat" => NotifyChannel.Chat,
        _ => NotifyChannel.Both,
    };
}

public class Toggle
{
    public bool Enabled { get; set; } = true;
}

public class ToggleChannel : Toggle
{
    public NotifyChannel Channel { get; set; } = NotifyChannel.Both;
}

/// <summary>
/// Гибкие настройки уведомлений клана. Сериализуются в Clan.NotificationSettingsJson.
/// Значения по умолчанию = текущее поведение (всё включено, канал «везде»), поэтому
/// существующие кланы без сохранённых настроек работают как раньше.
/// </summary>
public class NotificationSettings
{
    public ToggleChannel Reminders { get; set; } = new();
    public ToggleChannel WarStart { get; set; } = new();
    public Toggle FinalCall { get; set; } = new();
    public Toggle DailyReport { get; set; } = new();

    /// <summary>Поздравление в чат, когда игрок набирает 900 медалей за день (идеальный день).</summary>
    public Toggle PerfectDay { get; set; } = new();

    /// <summary>
    /// Язык, на котором бот пишет клану: "ru" | "uk" | "en". null/мусор — русский,
    /// то есть поведение кланов, заведённых до появления настройки, не меняется.
    /// Живёт здесь, а не отдельной колонкой, именно поэтому: JSON расширяется без миграции.
    /// </summary>
    public string? Language { get; set; }

    // JsonIgnore обязателен: Serialize() пишет все публичные свойства, и без него
    // в NotificationSettingsJson каждого клана уезжала бы вся таблица переводов —
    // сотня строк на язык вместо одного поля "language".

    /// <summary>Разобранный язык сообщений — с запасным вариантом на случай пустого значения.</summary>
    [JsonIgnore]
    public BotLang Lang => BotText.ParseLang(Language);

    /// <summary>Готовые тексты на языке клана.</summary>
    [JsonIgnore]
    public BotText Text => BotText.For(Lang);

    /// <summary>
    /// Во сколько заканчивается военный день у клана — минуты от полуночи UTC (0..1439).
    /// Задаёт глава в настройках. null — используем допущение по умолчанию (10:00 UTC).
    /// От этого времени считаются напоминания «за N часов», последний звонок и «до конца дня».
    /// </summary>
    public int? WarEndMinuteUtc { get; set; }

    /// <summary>Ближайшее (в будущем) время окончания дня по настройке главы. null — не задано.</summary>
    public DateTime? NextWarEndUtc(DateTime nowUtc)
    {
        if (WarEndMinuteUtc is not int m || m < 0 || m >= 1440) return null;
        var end = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, m / 60, m % 60, 0, DateTimeKind.Utc);
        return nowUtc < end ? end : end.AddDays(1);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Разбор из JSON клана. Null/битый JSON → настройки по умолчанию.</summary>
    public static NotificationSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new NotificationSettings();
        try
        {
            var s = JsonSerializer.Deserialize<NotificationSettings>(json, JsonOpts);
            if (s is null) return new NotificationSettings();
            s.Reminders ??= new ToggleChannel();
            s.WarStart ??= new ToggleChannel();
            s.FinalCall ??= new Toggle();
            s.DailyReport ??= new Toggle();
            return s;
        }
        catch { return new NotificationSettings(); }
    }

    public string Serialize() => JsonSerializer.Serialize(this, JsonOpts);
}
