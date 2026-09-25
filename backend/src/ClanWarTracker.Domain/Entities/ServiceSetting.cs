namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Настройка сервиса, которую владелец меняет на ходу: ключ и значение строкой.
///
/// Таблицей, а не конфигом: конфиг живёт в образе, и чтобы поменять в нём одну
/// строку, нужен передеплой. Настройки вроде «какие вкладки показывать внизу»
/// правят из панели и хотят увидеть результат сразу.
/// </summary>
public class ServiceSetting
{
    public int Id { get; set; }

    /// <summary>Ключ, например «tabs».</summary>
    public required string Key { get; set; }

    /// <summary>Значение. Сложные настройки кладём сюда JSON-ом.</summary>
    public required string Value { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
