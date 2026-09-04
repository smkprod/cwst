namespace ClanWarTracker.Application.Games;

/// <summary>
/// Ключ для подписи пропусков к картинкам-загадкам.
///
/// Отдельный интерфейс, а не IConfiguration в конструкторе: слою логики незачем знать,
/// откуда берётся секрет, а тестам — тащить за собой конфигурацию.
/// </summary>
public interface IPuzzleSecret
{
    string Value { get; }
}
