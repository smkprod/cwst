namespace ClanWarTracker.Application.Meta;

/// <summary>
/// Эмодзи, которые игрок может поставить себе аватаркой в клановом списке.
///
/// Список закрытый, и проверяет его сервер. Причина не в эстетике: аватарка едет рядом
/// с именем во все списки, а имена клан видит и в сообщениях бота. Свободное поле здесь —
/// это чужой текст в интерфейсе и в чате, поэтому принимаем только то, что сами предложили.
/// </summary>
public static class PlayerAvatars
{
    /// <summary>Разрешённые аватарки. Тот же набор показывает пикер во фронте.</summary>
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // Бойцы
        "⚔️", "🛡️", "🏹", "🗡️", "🪓", "💣", "🔥", "⚡",
        // Звери
        "🐉", "🐲", "🦁", "🐺", "🦅", "🦈", "🐻", "🦊", "🐸", "🦂", "🐍", "🦉",
        // Короны и награды
        "👑", "🏆", "🥇", "💎", "⭐", "🌟", "💪", "🎯",
        // Характер
        "😈", "👻", "💀", "🤖", "👽", "🤡", "🥷", "🧙", "🧊", "🌪️",
        // Спокойные
        "🍀", "🌙", "☀️", "🌊", "🍕", "🎮", "🎲", "🎸",
    };

    /// <summary>
    /// Приводит присланное значение к сохраняемому виду: null — снять аватарку,
    /// строка из списка — поставить. Всё остальное отвергается (false).
    /// </summary>
    public static bool TryNormalize(string? raw, out string? emoji)
    {
        emoji = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;   // явное «снять аватарку»

        var trimmed = raw.Trim();
        if (!Allowed.Contains(trimmed)) return false;

        emoji = trimmed;
        return true;
    }
}
