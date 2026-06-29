using System.Text.Json;

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
