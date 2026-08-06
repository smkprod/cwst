using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Что нового» — персональная дельта с прошлого визита в Mini App: сколько медалей набрал,
/// на сколько мест сместился, кто обошёл, сколько респектов получил, сколько колод осталось.
/// Считывается при входе и СРАЗУ переписывает снимок визита — следующий заход покажет
/// дельту от этого момента. Персонализация входа: каждый визит начинается с нового факта.
/// </summary>
public class GetWhatsNewUseCase(
    IClashRoyaleApi crApi,
    IClanRepository clans,
    IPlayerRepository players,
    IRespectRepository respects)
{
    public async Task<WhatsNewDto?> ExecuteAsync(long telegramUserId, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null || player.ClanId is not int clanId) return null;

        var clan = await clans.GetByIdAsync(clanId, ct);
        if (clan is null) return null;

        var war = await crApi.GetCurrentWarAsync(clan.ClanTag, ct);
        if (war is null) return null;

        // Текущий состав: в списке войны CR держит и ушедших — иначе ранг поедет
        Dictionary<string, string> members;
        try { members = await crApi.GetClanMemberRolesAsync(clan.ClanTag, ct); }
        catch { members = new(StringComparer.OrdinalIgnoreCase); }
        var rosterTags = WarRoster.CurrentMemberTags(war, members);
        var roster = war.Participants
            .Where(p => rosterTags.Contains(p.PlayerTag)
                        || string.Equals(p.PlayerTag, player.PlayerTag, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Fame)
            .ToList();

        var meIndex = roster.FindIndex(p =>
            string.Equals(p.PlayerTag, player.PlayerTag, StringComparison.OrdinalIgnoreCase));
        if (meIndex < 0) return null;

        var me = roster[meIndex];
        var rank = meIndex + 1;
        var isFirst = player.LastVisitAtUtc is null;
        var since = player.LastVisitAtUtc ?? DateTime.UtcNow;

        // Медали за неделю копятся, но на стыке недель счётчик обнуляется — отрицательную
        // дельту не показываем (это не «минус медали», а новая неделя).
        var fameDelta = Math.Max(0, me.Fame - (player.LastVisitFame ?? me.Fame));
        // Ранг: меньше = лучше, поэтому дельта «мест вверх» = старый − новый
        var rankDelta = (player.LastVisitRank ?? rank) - rank;

        // Кто обошёл, пока тебя не было: сосед сверху, если ранг ухудшился
        var passedBy = rankDelta < 0 && meIndex > 0 ? roster[meIndex - 1].Name : null;

        var (_, respectsSince) = await respects.CountForPlayerAsync(player.PlayerTag, since, ct);

        var decksLeft = war.IsWarDay ? Math.Max(0, 4 - me.DecksUsedToday) : 0;

        // Снимок визита обновляем сразу: следующий заход считает дельту от «сейчас»
        player.LastVisitAtUtc = DateTime.UtcNow;
        player.LastVisitFame = me.Fame;
        player.LastVisitRank = rank;
        await players.SaveChangesAsync(ct);

        return new WhatsNewDto(
            IsFirstVisit: isFirst,
            LastVisitAtUtc: isFirst ? null : since,
            FameDelta: fameDelta,
            RankDelta: rankDelta,
            Rank: rank,
            RespectsSince: respectsSince,
            PassedByName: passedBy,
            DecksLeftToday: decksLeft,
            BadgesEarned: []);
    }
}
