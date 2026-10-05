using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Всё о игроке для единой карточки: профиль из игры, колода последнего боя,
/// последние бои и то, что знает бот (спонсорство, место в мировом топе, разбор).
///
/// Раньше у каждого экрана была своя карточка со своим набором данных, и один и
/// тот же человек в поиске, в КВ и в топе выглядел как три разных.
/// </summary>
public class GetPlayerSheetUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    ITopPlayerRepository top,
    IPlayerBattleRepository battles,
    DuelUseCase duels)
{
    /// <summary>Сколько последних боёв показываем. Журнал хранит 25, больше десятка не листают.</summary>
    private const int BattlesShown = 10;

    /// <returns>null - игрока с таким тегом нет.</returns>
    public async Task<PlayerSheetDto?> ExecuteAsync(string playerTag, CancellationToken ct = default)
    {
        var info = await crApi.GetPlayerInfoAsync(playerTag, ct);
        if (info is null) return null;
        var tag = info.Tag;

        var log = new List<CrRecentBattle>();
        try { log = await crApi.GetRecentBattlesAsync(tag, ct); }
        catch { /* без боёв карточка всё равно полезна */ }

        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);

        string? role = null;
        if (!string.IsNullOrEmpty(info.ClanTag))
        {
            try { role = await crApi.GetPlayerClanRoleAsync(info.ClanTag, tag, ct); }
            catch { /* роль - украшение, не повод ронять карточку */ }
        }

        var now = DateTime.UtcNow;
        var linked = await players.GetByTagAsync(tag, ct);
        var inBot = linked?.TelegramUserId is not null;
        var sponsor = linked is not null && linked.IsSponsor(now);

        int? worldRank = null;
        var day = await top.LatestDayAsync(ct);
        if (day is not null) worldRank = (await top.FindAsync(day, tag, ct))?.Rank;

        var stored = inBot
            ? await battles.GetSinceAsync(tag, now - CollectPlayerBattlesUseCase.Keep, ct)
            : [];
        var storedWins = stored.Count(b => b.Result > 0);

        // Колода последнего настоящего боя - всегда восемь карт. «Текущая колода»
        // профиля у части игроков приходит неполной.
        var real = log.Where(CollectPlayerBattlesUseCase.Counts).OrderByDescending(b => b.BattleTimeUtc).ToList();
        var deckCards = real.FirstOrDefault()?.MyDeck ?? info.CurrentDeck;
        var deckKeys = deckCards.Select(MetaCard.Key).ToArray();
        var deck = deckKeys.Select(k => GetMetaDecksUseCase.Card(k, catalog)).ToList();

        MetaCardDto? favourite = null;
        if (info.CurrentFavouriteCard is { } favName)
        {
            var fav = catalog.Values.FirstOrDefault(c => string.Equals(c.Name, favName, StringComparison.OrdinalIgnoreCase));
            if (fav is not null) favourite = GetMetaDecksUseCase.Card(fav.Id, catalog);
        }

        var shown = log
            .Where(b => b.MyDeck.Count > 0)
            .OrderByDescending(b => b.BattleTimeUtc)
            .Take(BattlesShown)
            .Select(b => new SheetBattleDto(
                TimeUtc: DateTime.SpecifyKind(b.BattleTimeUtc, DateTimeKind.Utc).ToString("O"),
                Type: b.Type,
                Result: b.CrownsFor == b.CrownsAgainst ? 0 : b.Won ? 1 : -1,
                CrownsFor: b.CrownsFor,
                CrownsAgainst: b.CrownsAgainst,
                OpponentName: b.OpponentName,
                OpponentTag: b.OpponentTag,
                TrophyChange: b.TrophyChange,
                MyDeck: b.MyDeck.Select(c => GetMetaDecksUseCase.Card(MetaCard.Key(c), catalog)).ToList(),
                OpponentDeck: b.OpponentDeck.Select(c => GetMetaDecksUseCase.Card(MetaCard.Key(c), catalog)).ToList()))
            .ToList();

        DuelSheetDto? duel = null;
        try
        {
            if (await duels.GetSheetRankAsync(tag, ct) is { } r)
                duel = new DuelSheetDto(r.Trophies, r.Peak, r.League, r.Division, r.Wins, r.Losses, r.Place);
        }
        catch { /* лига - украшение карточки */ }

        var pol = info.CurrentPathOfLegend;
        return new PlayerSheetDto(
            PlayerTag: tag,
            Name: info.Name,
            ExpLevel: info.ExpLevel,
            ClanName: info.ClanName,
            ClanTag: info.ClanTag,
            Role: role,
            ArenaName: info.ArenaName,
            Trophies: info.Trophies,
            BestTrophies: info.BestTrophies,
            Rating: pol is { Trophies: > 0 } ? pol.Trophies : null,
            RatingLeague: pol is { LeagueNumber: > 0 } ? pol.LeagueNumber : null,
            RatingRank: pol is { Rank: > 0 } ? pol.Rank : null,
            BestRating: info.BestPathOfLegend is { Trophies: > 0 } best ? best.Trophies : null,
            Wins: info.Wins,
            Losses: info.Losses,
            ThreeCrownWins: info.ThreeCrownWins,
            BattleCount: info.BattleCount,
            WarDayWins: info.WarDayWins,
            ClanWarTrophies: info.ClanWarTrophies,
            CurrentStreak: info.CurrentWinLoseStreak,
            FavouriteCard: favourite,
            WorldRank: worldRank,
            InBot: inBot,
            IsSponsor: sponsor,
            BackgroundKey: sponsor ? SponsorBackground.NormalizePlayer(linked!.SponsorBackgroundKey) : null,
            BadgeKey: linked?.ShowcaseBadgeKey,
            BadgeLevel: linked?.ShowcaseBadgeLevel ?? 0,
            Deck: deck,
            DeckElixir: deck.Count == 0 ? 0 : Math.Round(deck.Average(c => c.Elixir), 1),
            DeckLink: GetMetaDecksUseCase.CopyLink(deckKeys),
            Battles: shown,
            Games30: stored.Count,
            WinPercent30: stored.Count == 0 ? 0 : Math.Round(100.0 * storedWins / stored.Count, 1),
            RoyaleApiUrl: $"https://royaleapi.com/player/{Uri.EscapeDataString(tag.TrimStart('#'))}",
            Duel: duel);
    }
}
