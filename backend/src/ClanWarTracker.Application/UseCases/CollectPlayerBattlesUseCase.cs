using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Копит бои привязанных игроков для личного разбора.
///
/// API хранит 25 последних боёв. Активный игрок сыгрывает их за вечер, поэтому
/// журнал читаем каждые несколько часов, а при открытии разбора - ещё раз, чтобы
/// самые свежие бои попали в него сразу.
/// </summary>
public class CollectPlayerBattlesUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    IPlayerBattleRepository battles)
{
    /// <summary>Сколько храним. Разбор смотрит месяц: дольше колоды и мета уже другие.</summary>
    public static readonly TimeSpan Keep = TimeSpan.FromDays(30);

    /// <summary>Журналы тянем по три параллельно: остальной бот ходит в API через тот же ключ.</summary>
    private const int Parallelism = 3;

    /// <summary>
    /// Какие бои берём. Дружеские, тренировки и 2х2 про другое: там не стараются
    /// или играют чужой колодой, и они размыли бы и проценты, и тильт.
    /// </summary>
    public static bool Counts(CrRecentBattle b) =>
        b.TeamTags.Count == 1 && b.OpponentTags.Count == 1
        && b.MyDeck.Count == 8 && b.OpponentDeck.Count == 8
        && !b.Type.Contains("friendly", StringComparison.OrdinalIgnoreCase)
        && !b.Type.Contains("training", StringComparison.OrdinalIgnoreCase)
        && !b.Type.Contains("clanMate", StringComparison.OrdinalIgnoreCase)
        && !b.Type.Contains("tutorial", StringComparison.OrdinalIgnoreCase);

    /// <summary>Читает журнал одного игрока и дописывает новые бои. Возвращает, сколько добавлено.</summary>
    public async Task<int> SyncAsync(string playerTag, CancellationToken ct = default)
    {
        var log = await crApi.GetRecentBattlesAsync(playerTag, ct);
        return await SaveAsync(playerTag, log, ct);
    }

    public record Summary(int Players, int Added, int Failed, int Purged);

    /// <summary>Проход по всем привязанным игрокам и чистка старого.</summary>
    public async Task<Summary> SyncAllAsync(CancellationToken ct = default)
    {
        var linked = await players.GetAllLinkedAsync(ct);
        var tags = linked.Select(p => p.PlayerTag).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        int added = 0, failed = 0;
        using var gate = new SemaphoreSlim(Parallelism);
        var results = await Task.WhenAll(tags.Select(async tag =>
        {
            await gate.WaitAsync(ct);
            try { return (Added: await SyncOneSafeAsync(tag, ct), Ok: true); }
            catch { return (Added: 0, Ok: false); }
            finally { gate.Release(); }
        }));
        foreach (var r in results)
        {
            added += r.Added;
            if (!r.Ok) failed++;
        }

        var purged = await battles.PurgeOlderThanAsync(DateTime.UtcNow - Keep, ct);
        return new Summary(tags.Count, added, failed, purged);
    }

    // Репозиторий на один DbContext, а DbContext не терпит параллельных запросов:
    // журналы тянем параллельно, а записываем по одному.
    private readonly SemaphoreSlim _write = new(1, 1);

    private async Task<int> SyncOneSafeAsync(string tag, CancellationToken ct)
    {
        var log = await crApi.GetRecentBattlesAsync(tag, ct);
        await _write.WaitAsync(ct);
        try { return await SaveAsync(tag, log, ct); }
        finally { _write.Release(); }
    }

    private Task<int> SaveAsync(string playerTag, List<CrRecentBattle> log, CancellationToken ct)
    {
        var rows = log
            .Where(Counts)
            .Select(b => ToRow(playerTag, b))
            .ToList();
        return battles.AddNewAsync(playerTag, rows, ct);
    }

    private static PlayerBattle ToRow(string playerTag, CrRecentBattle b) => new()
    {
        PlayerTag = playerTag,
        BattleTimeUtc = DateTime.SpecifyKind(b.BattleTimeUtc, DateTimeKind.Utc),
        Type = b.Type.Length > 48 ? b.Type[..48] : b.Type,
        Result = b.CrownsFor == b.CrownsAgainst ? 0 : b.Won ? 1 : -1,
        CrownsFor = b.CrownsFor,
        CrownsAgainst = b.CrownsAgainst,
        DeckKey = MetaCard.DeckKey(b.MyDeck.Select(MetaCard.Key)),
        OppDeckKey = MetaCard.DeckKey(b.OpponentDeck.Select(MetaCard.Key)),
        ElixirLeaked = b.ElixirLeaked,
        TrophyChange = b.TrophyChange,
    };
}
