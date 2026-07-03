using ClanWarTracker.Domain.Entities;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Текущий состав клана для военных уведомлений. CR API в списке войны (/currentriverrace)
/// держит и ушедших участников — за неделю их бывает &gt;50, из-за чего счётчики «не доиграли /
/// не привязали» раздувались. Берём актуальный состав из /members; если он недоступен
/// (API прилёг / лимит), запасной вариант — топ-50 самых активных участников войны, чтобы
/// счётчик всё равно не превышал размер клана. Та же логика, что и в GetClanStatusUseCase.
/// </summary>
public static class WarRoster
{
    private const int MaxClanMembers = 50;

    public static HashSet<string> CurrentMemberTags(
        WarStatus war, IReadOnlyDictionary<string, string> memberRoles)
    {
        if (memberRoles.Count > 0)
            return memberRoles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return war.Participants
            .OrderByDescending(p => p.DecksUsedToday)
            .ThenByDescending(p => p.DecksUsed)
            .ThenByDescending(p => p.Fame)
            .Take(MaxClanMembers)
            .Select(p => p.PlayerTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
