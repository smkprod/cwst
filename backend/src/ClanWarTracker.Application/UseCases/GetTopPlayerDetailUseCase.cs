using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Карточка игрока из топа: профиль, текущая колода и последние бои.
///
/// Работает для ЛЮБОГО тега, не только для попавших в снимок: место подставляется,
/// если человек в топе, но отсутствие места не повод отказывать в просмотре.
/// </summary>
public class GetTopPlayerDetailUseCase(IClashRoyaleApi crApi, ITopPlayerRepository top)
{
    /// <summary>Сколько боёв показываем. API отдаёт около двадцати пяти, больше взять негде.</summary>
    private const int BattlesShown = 15;

    public async Task<TopPlayerDetailDto?> ExecuteAsync(string playerTag, CancellationToken ct = default)
    {
        var info = await crApi.GetPlayerInfoAsync(playerTag, ct);
        if (info is null) return null;

        // Бои — отдельный запрос, и он может не прийти. Профиль без боёв показать
        // всё равно стоит: это большая часть карточки.
        List<CrRecentBattle> battles;
        try { battles = await crApi.GetRecentBattlesAsync(playerTag, ct); }
        catch { battles = []; }

        int? rank = null;
        var day = await top.LatestDayAsync(ct);
        if (day is not null)
        {
            var snapshot = await top.GetDayAsync(day, ct);
            rank = snapshot.FirstOrDefault(r =>
                string.Equals(r.PlayerTag, info.Tag, StringComparison.OrdinalIgnoreCase))?.Rank;
        }

        return new TopPlayerDetailDto(
            info.Tag, info.Name, info.ClanName, info.Trophies, info.BestTrophies, info.ExpLevel,
            info.Wins, info.Losses, info.ThreeCrownWins, rank,
            info.CurrentDeck.Select(Card).ToList(),
            battles.Take(BattlesShown).Select(b => new TopBattleDto(
                b.BattleTimeUtc.ToString("O"), b.Type, b.Won, b.CrownsFor, b.CrownsAgainst,
                b.OpponentName,
                b.MyDeck.Select(Card).ToList(),
                b.OpponentDeck.Select(Card).ToList())).ToList());
    }

    private static TopDeckCardDto Card(CrDeckCard c) => new(c.Id, c.Name, c.IconUrl);
}
