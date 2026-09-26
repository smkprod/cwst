using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

public enum ClanPageError
{
    PlayerNotLinked,
    NotMyClan,
    ClanNotFound,
    NotAllowed,
    UnknownDesign,
    DesignNeedsSponsor,
    MottoTooLong,
}

/// <summary>
/// Настройка страницы клана: оформление и девиз.
///
/// Право на это даётся главе и действующему спонсору клана. Проверяется здесь, а
/// не на чтении: страницу открывают постоянно, а перекрашивают раз в месяц, и
/// платить походом в Clash Royale за каждый просмотр чужого клана незачем.
/// </summary>
public class SetClanPageUseCase(
    IPlayerRepository players,
    IClanRepository clans,
    IClashRoyaleApi crApi)
{
    public async Task<ClanPageError?> ExecuteAsync(
        long telegramUserId, int clanId, string? designKey, string? motto, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return ClanPageError.PlayerNotLinked;
        if (player.ClanId != clanId) return ClanPageError.NotMyClan;

        var clan = await clans.GetByIdAsync(clanId, ct);
        if (clan is null) return ClanPageError.ClanNotFound;

        var now = DateTime.UtcNow;
        var iAmSponsor = player.IsSponsor(now);
        if (!iAmSponsor && !await IsLeaderAsync(clan, player, ct)) return ClanPageError.NotAllowed;

        if (designKey is not null)
        {
            if (!ClanPageDesign.IsKnown(designKey)) return ClanPageError.UnknownDesign;

            // Платный дизайн открывает наличие спонсора в клане, а не в кресле: глава
            // без спонсорства вправе выбрать его, если клан спонсора содержит.
            if (ClanPageDesign.NeedsSponsor(designKey) && designKey != clan.PageDesignKey)
            {
                var clanPlayers = await players.GetByClanIdAsync(clanId, ct);
                if (!clanPlayers.Any(p => p.IsSponsor(now))) return ClanPageError.DesignNeedsSponsor;
            }

            clan.PageDesignKey = designKey;
        }

        if (motto is not null)
        {
            var trimmed = motto.Trim();
            if (trimmed.Length > ClanPageDesign.MaxMottoLength) return ClanPageError.MottoTooLong;
            // Пустая строка — это «девиза нет», а не девиз из ничего: иначе страница
            // рисовала бы под названием пустую полосу.
            clan.Motto = trimmed.Length == 0 ? null : trimmed;
        }

        await clans.SaveChangesAsync(ct);
        return null;
    }

    private async Task<bool> IsLeaderAsync(Clan clan, Player player, CancellationToken ct)
    {
        try
        {
            return await crApi.GetPlayerClanRoleAsync(clan.ClanTag, player.PlayerTag, ct) is "leader";
        }
        catch
        {
            return false;
        }
    }
}
