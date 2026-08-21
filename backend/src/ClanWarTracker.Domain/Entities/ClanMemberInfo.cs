namespace ClanWarTracker.Domain.Entities;

/// <summary>
/// Участник клана по данным /clans/{tag}/members. Берём из того же кэшированного
/// ответа, что и роли, — кубки достаются без дополнительных запросов к CR API.
/// </summary>
public record ClanMemberInfo(string Tag, string Role, int Trophies);
