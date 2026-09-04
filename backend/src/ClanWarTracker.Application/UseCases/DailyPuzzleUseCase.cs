using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Games;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Карта дня»: одна загадка в сутки, общая для всех.
///
/// Правила намеренно жёсткие в одном месте — переиграть день нельзя. Запись о попытках
/// создаётся на первом же ответе, поэтому выйти из приложения после промаха и вернуться
/// к чистой загадке не выйдет. Без этого игра решается перебором четырёх вариантов
/// за десять секунд, и любая награда за неё обесценивается.
/// </summary>
public class DailyPuzzleUseCase(IClashRoyaleApi crApi, IPuzzleRepository puzzles, IPuzzleSecret secret)
{
    /// <summary>Сколько попыток даём. Столько же уровней приближения у картинки.</summary>
    public const int MaxAttempts = 3;

    public async Task<DailyPuzzleDto?> GetAsync(Player player, CancellationToken ct = default)
    {
        var day = DailyCard.DayNumber(DateTime.UtcNow);

        var catalog = await crApi.GetAllCardsAsync(ct);
        var cards = catalog.Values.ToList();
        var answer = DailyCard.Pick(cards, day);
        if (answer is null) return null;

        var result = await puzzles.GetAsync(player.Id, day, ct);
        return Describe(player.Id, day, answer, cards, result,
            await puzzles.GetStreakAsync(player.Id, day, ct), secret.Value);
    }

    /// <summary>
    /// Ответ игрока. Возвращает null, если справочник карт недоступен, — тогда клиент
    /// просто не покажет игру, а не запишет игроку промах из-за чужой недоступности.
    /// </summary>
    public async Task<DailyPuzzleDto?> GuessAsync(Player player, int cardId, CancellationToken ct = default)
    {
        var day = DailyCard.DayNumber(DateTime.UtcNow);

        var catalog = await crApi.GetAllCardsAsync(ct);
        var cards = catalog.Values.ToList();
        var answer = DailyCard.Pick(cards, day);
        if (answer is null) return null;

        var result = await puzzles.GetAsync(player.Id, day, ct)
                     ?? new PuzzleResult { PlayerId = player.Id, Day = day, PlayedAtUtc = DateTime.UtcNow };

        // День закрыт — молча отдаём текущее состояние. Ошибку показывать не за что:
        // так же выглядит обычное открытие приложения второй раз за день.
        if (!Finished(result))
        {
            // Ответ обязан быть одним из показанных вариантов: иначе можно прислать
            // весь справочник по одному и угадать, не глядя на картинку.
            var options = DailyCard.Options(cards, answer, day);
            if (options.Any(o => o.Id == cardId))
            {
                result.Attempts++;
                if (cardId == answer.Id)
                {
                    result.Solved = true;
                    // 3 очка за первую попытку, 2 за вторую, 1 за третью
                    result.Points = MaxAttempts - result.Attempts + 1;
                }
                await puzzles.SaveAsync(result, ct);
            }
        }

        return Describe(player.Id, day, answer, cards, result,
            await puzzles.GetStreakAsync(player.Id, day, ct), secret.Value);
    }

    private static bool Finished(PuzzleResult r) => r.Solved || r.Attempts >= MaxAttempts;

    private static DailyPuzzleDto Describe(
        int playerId, int day, CrCatalogCard answer, List<CrCatalogCard> cards,
        PuzzleResult? result, int streak, string secret)
    {
        var attempts = result?.Attempts ?? 0;
        var done = result is not null && Finished(result);

        return new DailyPuzzleDto(
            Day: day,
            Attempt: Math.Min(attempts + 1, MaxAttempts),
            MaxAttempts: MaxAttempts,
            // Уровень приближения = номер текущей попытки. Доигравшему показываем
            // самый широкий кадр: скрывать картинку после ответа уже незачем.
            Level: done ? MaxAttempts : Math.Min(attempts + 1, MaxAttempts),
            Solved: result?.Solved ?? false,
            Finished: done,
            Points: result?.Points ?? 0,
            Streak: streak,
            ImageToken: PuzzleToken.Create(playerId, day, secret),
            Options: DailyCard.Options(cards, answer, day)
                .Select(c => new PuzzleOptionDto(c.Id, c.Name, c.IconUrl))
                .ToList(),
            // Правильный ответ уходит клиенту ТОЛЬКО когда день доигран, иначе его
            // видно в ответе сервера, и игра ломается открытыми инструментами браузера.
            AnswerCardId: done ? answer.Id : null,
            AnswerName: done ? answer.Name : null,
            AnswerIconUrl: done ? answer.IconUrl : null);
    }
}
