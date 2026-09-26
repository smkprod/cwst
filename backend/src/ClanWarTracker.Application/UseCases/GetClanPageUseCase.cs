using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Страница чужого клана.
///
/// Считается из того же кэша, что и сама Аллея: место клана обязано совпадать с
/// местом в списке до цифры, а второй независимый расчёт рано или поздно разойдётся
/// с первым — и разойдётся незаметно, потому что сравнивать их будет только игрок.
/// </summary>
public class GetClanPageUseCase(
    GetHallOfFameUseCase hall,
    IClanRepository clans,
    IPlayerRepository players,
    IWarSnapshotRepository snapshots,
    IClashRoyaleApi crApi)
{
    /// <summary>Сколько участников показываем. Состав клана — полсотни, и это весь клан.</summary>
    private const int MaxMembers = 50;

    public async Task<ClanPageDto?> ExecuteAsync(int clanId, long viewerTelegramId, CancellationToken ct = default)
    {
        var clan = await clans.GetByIdAsync(clanId, ct);
        if (clan is null) return null;

        var data = await hall.LoadAsync(ct);
        var row = data?.Clans.FirstOrDefault(c => c.ClanId == clanId);

        var viewer = await players.GetByTelegramIdAsync(viewerTelegramId, ct);
        var isMine = viewer?.ClanId == clanId;

        // Участники — из общего расчёта Аллеи, по тегу клана. Тег, а не ClanId:
        // в зачёте игрок привязан к клану, за который играл, а ClanId в базе
        // означает «где он сейчас». У сменившего клан это разные вещи, и на
        // странице клана должны стоять те, кто набивал медали за него.
        List<ClanPageMemberDto> members = data is null
            ? []
            : data.Players
                .Where(p => p.ClanTag is not null
                            && string.Equals(p.ClanTag, clan.ClanTag, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.SeasonFame)
                .Take(MaxMembers)
                .Select((p, i) => new ClanPageMemberDto(
                    Rank: i + 1,
                    HallRank: p.Rank,
                    PlayerTag: p.PlayerTag,
                    Name: p.Name,
                    SeasonFame: p.SeasonFame,
                    WeeksPlayed: p.WeeksPlayed,
                    BadgeKey: p.BadgeKey,
                    BadgeLevel: p.BadgeLevel,
                    IsSponsor: p.IsSponsor,
                    BackgroundKey: p.BackgroundKey))
                .ToList();

        List<SeasonWeekDto> weeks = data is null ? [] : await WeeksAsync(clanId, data.SeasonId, ct);

        var now = DateTime.UtcNow;
        var clanPlayers = await players.GetByClanIdAsync(clanId, ct);
        var sponsors = clanPlayers.Count(p => p.IsSponsor(now));

        var canEdit = isMine && viewer is not null && await CanEditAsync(clan, viewer, now, ct);

        // Что показать в выборе: клану со спонсором — весь каталог, иначе только
        // бесплатное плюс то, что клан уже выбрал. Последнее важно: спонсорство
        // кончается, а страница остаётся, и отсутствие текущего дизайна в списке
        // выглядело бы как его молчаливое снятие.
        var designKey = ClanPageDesign.Normalize(clan.PageDesignKey);
        List<string> available = [];
        if (canEdit)
        {
            available = sponsors > 0
                ? [.. ClanPageDesign.All]
                : [.. ClanPageDesign.Free.Append(designKey).Distinct()];
        }

        return new ClanPageDto(
            ClanId: clan.Id,
            ClanTag: clan.ClanTag,
            ClanName: clan.Name,
            Motto: clan.Motto,
            Rank: row?.Rank ?? 0,
            ClansCounted: data?.ClansCounted ?? 0,
            SeasonId: data?.SeasonId ?? 0,
            SeasonFame: row?.SeasonFame ?? 0,
            WeeksPlayed: row?.WeeksPlayed ?? 0,
            MembersInSeason: members.Count,
            SponsorCount: sponsors,
            BackgroundKey: row?.BackgroundKey,
            DesignKey: designKey,
            AcceptsMail: clan.AcceptsClanMail,
            IsMine: isMine,
            CanEdit: canEdit,
            AvailableDesigns: available,
            Members: members,
            Weeks: weeks);
    }

    /// <summary>
    /// Слава клана по неделям сезона — маленький график на странице.
    ///
    /// Финал недели считается так же, как в зачёте: слава за неделю только растёт,
    /// значит максимум и есть итог.
    /// </summary>
    private async Task<List<SeasonWeekDto>> WeeksAsync(int clanId, int seasonId, CancellationToken ct)
    {
        var season = await snapshots.GetBySeasonAsync(clanId, seasonId, ct);
        return season
            .GroupBy(s => s.SectionIndex)
            .Select(g => new SeasonWeekDto(g.Key, g.Max(s => s.TotalFame)))
            .Where(w => w.Fame > 0)
            .OrderBy(w => w.SectionIndex)
            .ToList();
    }

    /// <summary>
    /// Кто правит страницу: глава клана и действующий спонсор из этого клана.
    ///
    /// Со-руководителей нет по той же причине, что и в сообщениях между кланами:
    /// их бывает до четырёх, и страница клана превратилась бы в то, что последний
    /// из них перекрасил. Роль спрашиваем у Clash Royale только для своего клана —
    /// там ответ уже лежит в кэше состава, который приложение и так тянет.
    /// </summary>
    private async Task<bool> CanEditAsync(Clan clan, Player viewer, DateTime now, CancellationToken ct)
    {
        if (viewer.IsSponsor(now)) return true;
        try
        {
            return await crApi.GetPlayerClanRoleAsync(clan.ClanTag, viewer.PlayerTag, ct) is "leader";
        }
        catch
        {
            // Clash Royale молчит — кнопку не показываем. Ошибиться в сторону
            // «не дали настроить» дешевле, чем отрисовать кнопку, которая потом
            // откажет на сохранении.
            return false;
        }
    }
}
