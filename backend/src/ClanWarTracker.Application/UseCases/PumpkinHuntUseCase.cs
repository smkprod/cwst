using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Охота на тыквы» - вторая часть Хэллоуина. 13 тыкв спрятаны по разделам приложения,
/// где они лежат, знает фронт; сервер только помнит находки и выдаёт награду за все 13.
/// Длится до конца ивента: все тыквы открыты сразу, подсказку к следующей даёт призрак.
/// </summary>
public class PumpkinHuntUseCase(IPumpkinRepository pumpkins, IPlayerRepository players, PlusAccess plus)
{
    public const int Total = 13;

    /// <summary>Нашёл все 13 - столько дней Плюса.</summary>
    public const int RewardPlusDays = 3;

    /// <summary>Начало охоты: 30 октября, полночь по Киеву.</summary>
    public static readonly DateTime StartUtc = new(2026, 10, 29, 22, 0, 0, DateTimeKind.Utc);

    public static DateTime EndUtc => HalloweenUseCase.EventEndUtc;

    public static bool Open(DateTime nowUtc) => nowUtc >= StartUtc && nowUtc < EndUtc;

    /// <param name="FirstFinishers">Первые, кто нашёл все тыквы, - имена в порядке находки.</param>
    public record Status(bool Open, DateTime EndUtc, int Total, int RewardPlusDays, List<int> Found,
        int Hunters, int Finishers, List<string> FirstFinishers);

    public async Task<Status> StatusAsync(long telegramUserId, CancellationToken ct = default)
    {
        var found = await pumpkins.GetFoundAsync(telegramUserId, ct);
        var finishers = await pumpkins.FinishersAsync(Total, ct);
        var names = new List<string>();
        foreach (var (tg, _) in finishers.Take(5))
            names.Add((await players.GetByTelegramIdAsync(tg, ct))?.Name ?? "?");
        return new Status(Open(DateTime.UtcNow), EndUtc, Total, RewardPlusDays, found,
            await pumpkins.HuntersAsync(ct), finishers.Count, names);
    }

    /// <param name="IsNew">Тыква найдена впервые.</param>
    /// <param name="Completed">Это была последняя - собраны все.</param>
    /// <param name="RewardUntil">До какого числа Плюс после награды; null - награды не было.</param>
    public record FindResult(bool IsNew, int FoundCount, bool Completed, DateTime? RewardUntil, List<int> Found);

    /// <summary>Тыква найдена. null - охота закрыта или такой тыквы нет.</summary>
    public async Task<FindResult?> FindAsync(long telegramUserId, int pumpkinId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (!Open(now) || pumpkinId is < 1 or > Total) return null;

        var isNew = await pumpkins.TryAddAsync(telegramUserId, pumpkinId, now, ct);
        var found = await pumpkins.GetFoundAsync(telegramUserId, ct);
        var completed = isNew && found.Count == Total;
        DateTime? until = null;
        // Награда - ровно на тринадцатой: вставка уникальна, тринадцатой она бывает один раз
        if (completed)
        {
            var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
            until = await plus.GrantAsync(telegramUserId, RewardPlusDays, Entitlement.Sources.Pumpkins, player?.PlayerTag, 0, null, ct);
        }
        return new FindResult(isNew, found.Count, completed, until, found);
    }
}
