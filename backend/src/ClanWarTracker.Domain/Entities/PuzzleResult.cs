namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Результат игрока в «Карте дня» за один день.
///
/// Запись появляется на ПЕРВОЙ попытке, а не после победы: иначе выйдя из приложения
/// после промаха, человек вернулся бы к чистой загадке и подобрал ответ перебором.
/// </summary>
public class PuzzleResult
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>Номер загадки — он же порядковый день от эпохи (см. DailyCard).</summary>
    public int Day { get; set; }

    /// <summary>Сколько попыток потрачено, 1..3.</summary>
    public int Attempts { get; set; }

    /// <summary>Угадал ли в итоге. false при Attempts=3 — попытки кончились.</summary>
    public bool Solved { get; set; }

    /// <summary>Очки: 3 за первую попытку, 2 за вторую, 1 за третью, 0 за провал.</summary>
    public int Points { get; set; }

    public DateTime PlayedAtUtc { get; set; }
}
