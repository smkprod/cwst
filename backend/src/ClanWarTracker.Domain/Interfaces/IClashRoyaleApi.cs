using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Domain.Interfaces;

public interface IClashRoyaleApi
{
    Task<WarStatus?> GetCurrentWarAsync(string clanTag, CancellationToken ct = default);
    Task<string?> GetPlayerNameAsync(string playerTag, CancellationToken ct = default);
    Task<string?> GetClanNameAsync(string clanTag, CancellationToken ct = default);

    /// <summary>Тег клана, в котором игрок состоит прямо сейчас. null — игрок не найден или без клана.</summary>
    Task<string?> GetPlayerClanTagAsync(string playerTag, CancellationToken ct = default);
}
