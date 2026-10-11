namespace ClanWarTracker.Application.Notifications;

/// <summary>
/// Публичные адреса нарисованных карточек.
///
/// Логике незачем знать, на каком домене живёт сервис, а конфигурацию в use case
/// тащить не хочется. Реализация лежит в Infrastructure и читает PUBLIC_BASE_URL.
/// </summary>
public interface ICardUrls
{
    /// <summary>
    /// Адрес карточки: kind — «war», «profile», «clan», «deck», «perfect».
    /// null — публичный адрес не настроен, картинку слать некуда.
    /// </summary>
    string? Card(string kind, string playerTag);
}
