using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Отслеживание игровых турниров Clash Royale: организатор добавляет турнир по тегу,
/// бот тянет живые данные из CR API (/tournaments/{tag}) и показывает таблицу.
/// Никаких выдуманных данных — всё live из API; в БД лишь тег, пароль и кто добавил.
/// </summary>
public class GameTournamentService(IGameTournamentRepository repo, IClashRoyaleApi crApi)
{
    public enum AddError { None, NotFound, AlreadyTracked, BadTag }

    public record AddResult(AddError Error, GameTournamentDto? Tournament);

    /// <summary>Добавить турнир по тегу. Проверяет существование через CR API.</summary>
    public async Task<AddResult> AddAsync(string rawTag, string? password, long userId, string userName,
        CancellationToken ct = default)
    {
        var tag = NormalizeTag(rawTag);
        if (tag.Length < 4) return new AddResult(AddError.BadTag, null);

        if (await repo.GetByTagAsync(tag, ct) is not null)
            return new AddResult(AddError.AlreadyTracked, null);

        var live = await crApi.GetTournamentAsync(tag, ct);
        if (live is null) return new AddResult(AddError.NotFound, null);

        var entity = new GameTournament
        {
            TournamentTag = tag,
            Name = Trim(live.Name, 120),
            Password = string.IsNullOrWhiteSpace(password) ? null : Trim(password.Trim(), 64),
            CreatorTelegramUserId = userId,
            CreatorName = Trim(userName, 64),
            CreatedAtUtc = DateTime.UtcNow,
        };
        await repo.AddAsync(entity, ct);
        await repo.SaveChangesAsync(ct);

        return new AddResult(AddError.None, ToDto(entity, live, userId));
    }

    /// <summary>Список всех отслеживаемых турниров с живыми данными.</summary>
    public async Task<List<GameTournamentDto>> ListAsync(long userId, CancellationToken ct = default)
    {
        var tracked = await repo.GetAllAsync(ct);
        var result = new List<GameTournamentDto>(tracked.Count);
        foreach (var t in tracked)
        {
            CrTournament? live = null;
            try { live = await crApi.GetTournamentAsync(t.TournamentTag, ct); }
            catch { /* API временно недоступен — покажем без живых данных */ }
            result.Add(ToDto(t, live, userId));
        }
        return result;
    }

    /// <summary>Один турнир с живой таблицей. null — записи нет.</summary>
    public async Task<GameTournamentDto?> GetAsync(int id, long userId, CancellationToken ct = default)
    {
        var t = await repo.GetByIdAsync(id, ct);
        if (t is null) return null;
        CrTournament? live = null;
        try { live = await crApi.GetTournamentAsync(t.TournamentTag, ct); }
        catch { /* без живых данных */ }
        return ToDto(t, live, userId);
    }

    /// <summary>Удалить запись (только тот, кто добавил, или владелец сервиса).</summary>
    public async Task<bool> RemoveAsync(int id, long userId, bool isOwner, CancellationToken ct = default)
    {
        var t = await repo.GetByIdAsync(id, ct);
        if (t is null) return false;
        if (!isOwner && t.CreatorTelegramUserId != userId) return false;
        await repo.RemoveAsync(t, ct);
        await repo.SaveChangesAsync(ct);
        return true;
    }

    private static GameTournamentDto ToDto(GameTournament t, CrTournament? live, long userId) =>
        new(t.Id, t.TournamentTag, t.Password, t.CreatorName,
            IsCreator: t.CreatorTelegramUserId == userId,
            Live: live is null ? null : ToLive(live));

    private static GameTournamentLiveDto ToLive(CrTournament t)
    {
        var now = DateTime.UtcNow;
        int? startsIn = null, endsIn = null;
        if (t.Status == "IN_PREPARATION" && t.CreatedTime is { } created)
            startsIn = Math.Max(0, (int)(created.AddSeconds(t.PreparationDuration) - now).TotalSeconds);
        if (t.Status == "IN_PROGRESS" && t.StartedTime is { } started)
            endsIn = Math.Max(0, (int)(started.AddSeconds(t.Duration) - now).TotalSeconds);

        return new GameTournamentLiveDto(
            t.Name, t.Description, t.Status, t.Capacity, t.MaxCapacity, t.LevelCap,
            t.FirstPlaceCardPrize, t.GameMode, startsIn, endsIn,
            t.Members.Select(m => new GameTournamentMemberDto(m.Rank, m.Name, m.Score, m.ClanName)).ToList());
    }

    /// <summary>Тег Supercell: верхний регистр, ведущий #, частая опечатка O→0.</summary>
    private static string NormalizeTag(string tag)
    {
        tag = tag.Trim().ToUpperInvariant().Replace("O", "0");
        return tag.StartsWith('#') ? tag : "#" + tag;
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
