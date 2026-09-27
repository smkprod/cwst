using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Личный разбор боёв: утечка эликсира, свои колоды против топа, когда играешь
/// лучше, тильт и против чего проигрываешь.
///
/// Уровни карт сознательно не разбираем: у активных игроков почти всё прокачано,
/// и совет «докачай карту» ничего бы не дал.
/// </summary>
public class GetBattleAnalysisUseCase(
    IClashRoyaleApi crApi,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IMetaRepository meta,
    CollectPlayerBattlesUseCase collect,
    PlusAccess plus,
    IServiceSettingRepository settings)
{
    /// <summary>Своих колод показываем не больше - остальные сыграны по разу-два.</summary>
    private const int DecksShown = 3;

    /// <summary>Колода, сыгранная меньше раз, - проба, а не «моя колода».</summary>
    private const int MinDeckGames = 3;

    /// <summary>Сколько карт «против чего проигрываешь» показываем с Плюсом и без него.</summary>
    private const int ToughShownPlus = 6;
    private const int ToughShownFree = 1;

    /// <returns>null - игрок не привязан.</returns>
    public async Task<BattleAnalysisDto?> ExecuteAsync(long telegramUserId, int tzOffsetMinutes, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return null;
        var tag = player.PlayerTag;

        // Свежие бои - сразу, не дожидаясь воркера: человек только что сыграл и
        // открыл разбор ровно ради этих боёв. Не вышло - разбираем накопленное.
        try { await collect.SyncAsync(tag, ct); }
        catch { /* API игры недоступен - покажем то, что уже есть */ }

        var list = await battles.GetSinceAsync(tag, DateTime.UtcNow - CollectPlayerBattlesUseCase.Keep, ct);
        var totals = BattleAnalyzer.Count(list);

        // Триал - в момент, когда разбору уже есть что показать, а не при привязке:
        // три дня Плюса на пустой истории ничего бы человеку не дали.
        var trialStarted = false;
        try { trialStarted = await plus.TryStartTrialAsync(telegramUserId, tag, list.Count, ct); }
        catch { /* триал - подарок, его сбой не должен ронять сам разбор */ }
        var access = await plus.GetAsync(telegramUserId, ct);
        var offer = await PlusSales.ReadAsync(settings, ct);

        var window = await MetaWindow.LoadAsync(meta, ct);
        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);

        var leak = BattleAnalyzer.ElixirLeak(list);
        var tilt = BattleAnalyzer.Tilt(list);

        var decks = BattleAnalyzer.Decks(list)
            .Where(d => d.Games >= MinDeckGames)
            .Take(DecksShown)
            .Select(d => MyDeck(d, window, catalog))
            .ToList();

        var tough = BattleAnalyzer.ToughCards(list, take: ToughShownPlus)
            .Select(c => new ToughCardDto(
                GetMetaDecksUseCase.Card(c.CardKey, catalog),
                c.Games,
                BattleAnalyzer.Percent(c.Wins, c.Games),
                Math.Round(c.Delta * 100, 1)))
            .ToList();
        var weekdays = BattleAnalyzer.Weekdays(list, tzOffsetMinutes)
            .Select(s => new TimeSlotDto(s.Key, s.Games, BattleAnalyzer.Percent(s.Wins, s.Games)))
            .ToList();

        // Без Плюса разбор короткий: одна худшая карта, одна колода без контр, без
        // дней недели. Режем здесь, на сервере: спрятанное на клиенте всё равно
        // уезжало бы в ответе, и открыть его мог бы любой, кто заглянет в запрос.
        ReviewLockedDto? locked = null;
        if (!access.Unlocked)
        {
            var hiddenCounters = decks.Sum(d => d.MetaCounters.Count);
            locked = new ReviewLockedDto(
                ToughCards: Math.Max(0, tough.Count - ToughShownFree),
                Decks: Math.Max(0, decks.Count - 1),
                Counters: hiddenCounters,
                Weekdays: weekdays.Any(w => w.Games > 0));
            tough = tough.Take(ToughShownFree).ToList();
            decks = decks.Take(1).Select(d => d with { MetaCounters = [] }).ToList();
            weekdays = [];
        }

        return new BattleAnalysisDto(
            PlayerTag: tag,
            Games: totals.Games,
            Wins: totals.Wins,
            Losses: totals.Losses,
            Draws: totals.Draws,
            WinPercent: BattleAnalyzer.Percent(totals.Wins, totals.Games),
            SinceUtc: list.Count == 0 ? null : list[0].BattleTimeUtc.ToString("O"),
            Elixir: leak is null ? null : new ElixirLeakDto(leak.Wins, leak.Losses, leak.All, leak.Games),
            Decks: decks,
            DayParts: BattleAnalyzer.DayParts(list, tzOffsetMinutes)
                .Select(s => new TimeSlotDto(s.Key, s.Games, BattleAnalyzer.Percent(s.Wins, s.Games)))
                .ToList(),
            Weekdays: weekdays,
            Tilt: list.Count == 0 ? null : new TiltDto(
                tilt.AfterTwoLosses,
                BattleAnalyzer.Percent(tilt.AfterTwoLossesWins, tilt.AfterTwoLosses),
                tilt.AfterWin,
                BattleAnalyzer.Percent(tilt.AfterWinWins, tilt.AfterWin),
                tilt.LongestLossStreak),
            ToughCards: tough,
            MetaBattles: window?.Battles ?? 0,
            Access: new ReviewAccessDto(
                Paywall: access.Paywall,
                Unlocked: access.Unlocked,
                Active: access.Active,
                Until: access.Until?.ToString("O"),
                Source: access.Source,
                TrialStarted: trialStarted,
                TrialUsed: access.TrialUsed,
                TrialDays: offer.TrialDays,
                TrialMinBattles: PlusSales.TrialMinBattles),
            Locked: locked);
    }

    private static MyDeckDto MyDeck(
        BattleAnalyzer.DeckUse d,
        MetaWindow.Data? window,
        Dictionary<int, CrCatalogCard> catalog)
    {
        var keys = MetaCard.ParseDeckKey(d.Key);
        var closest = window is null ? null : BattleAnalyzer.ClosestMetaDeck(keys, window.Decks);

        // Контры считаем по своей колоде, а не по похожей из меты: пары «карта
        // против карты» есть для любой колоды, даже которой в топе нет вовсе.
        List<MetaCounterDto> counters = window is null
            ? []
            : MetaCounters.For(keys, window.Pairs)
                .Select(c => new MetaCounterDto(
                    GetMetaDecksUseCase.Card(c.OppKey, catalog),
                    Math.Round(c.WinRate * 100, 1),
                    Math.Round(c.Delta * 100, 1),
                    c.Games))
                .ToList();

        return new MyDeckDto(
            Cards: keys.Select(k => GetMetaDecksUseCase.Card(k, catalog)).ToList(),
            Games: d.Games,
            Wins: d.Wins,
            WinPercent: BattleAnalyzer.Percent(d.Wins, d.Games),
            MetaWinPercent: closest is null ? null : BattleAnalyzer.Percent(closest.Value.Wins, closest.Value.Games),
            MetaSharedCards: closest?.Shared ?? 0,
            MetaGames: closest?.Games ?? 0,
            MetaCounters: counters,
            CopyLink: GetMetaDecksUseCase.CopyLink(keys));
    }
}
