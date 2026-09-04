namespace ClanWarTracker.Application.DTOs;

/// <summary>Один вариант ответа в «Карте дня».</summary>
public record PuzzleOptionDto(int CardId, string Name, string IconUrl);

/// <summary>
/// Состояние сегодняшней загадки для конкретного игрока.
///
/// Правильный ответ появляется в полях Answer* только после того, как день доигран:
/// до этого он не должен покидать сервер вообще, иначе игра решается вкладкой «сеть»
/// в браузере.
/// </summary>
public record DailyPuzzleDto(
    int Day,                 // номер загадки, он же её публичное имя: «Карта дня #47»
    int Attempt,             // какая попытка идёт сейчас, 1..MaxAttempts
    int MaxAttempts,
    int Level,               // уровень приближения картинки
    bool Solved,
    bool Finished,           // угадал или потратил все попытки
    int Points,
    int Streak,              // дней подряд с угаданной картой
    /// <summary>
    /// Подписанный пропуск к картинке: /api/img/puzzle/{ImageToken}.jpg. Уровень
    /// приближения в адресе не передаётся намеренно — иначе достаточно попросить
    /// сразу третий и увидеть почти весь арт.
    /// </summary>
    string ImageToken,
    IReadOnlyList<PuzzleOptionDto> Options,
    int? AnswerCardId,
    string? AnswerName,
    string? AnswerIconUrl);
