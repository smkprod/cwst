using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Включение и выключение трекера боёв - из приложения, командой /tracker и кнопками
/// под его сообщениями. Одно место, чтобы все три пути делали одно и то же.
/// </summary>
public class TrackerActionsUseCase(
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IPlayerBattleRepository battles,
    IClanRepository clans,
    ISentNotificationRepository sentLog,
    IServiceSettingRepository settings,
    INotificationSender sender)
{
    /// <summary>Отметки для панели владельца: сколько выключили и сколько нажали «🔕».</summary>
    public const string OffKind = "trkoff";
    public const string MuteKind = "trkmute";

    /// <param name="Available">Трекер открыт этому человеку (нет закрытого теста или он в нём).</param>
    public record State(bool Available, bool Enabled, bool DmBlocked, bool MutedToday);

    /// <param name="Rows">Кнопки под обновлённым сообщением; пусто - без кнопок.</param>
    /// <param name="Toast">Ответ всплывающим окном, а сообщение не трогать (карточка захода остаётся).</param>
    public record Result(string Text, IReadOnlyList<IReadOnlyList<BotButton>> Rows, bool Toast = false);

    public async Task<State> GetAsync(long tg, CancellationToken ct = default)
    {
        var beta = await BattleTrackerUseCase.BetaAsync(settings, ct);
        var prefs = await alertPrefs.GetAsync(tg, ct);
        return ToState(prefs, BattleTrackerUseCase.Allowed(beta, tg));
    }

    private static State ToState(PlayerAlertPrefs? p, bool available) => new(
        available,
        p?.TrackerEnabled ?? false,
        p?.DmBlocked ?? false,
        p?.TrackerMutedUntilUtc > DateTime.UtcNow);

    /// <summary>
    /// Включить или выключить. null - игрок не привязан (трекеру нечего читать).
    /// </summary>
    /// <param name="notify">Написать в личку «трекер включён» - когда включили из приложения.</param>
    public async Task<State?> SetAsync(
        long tg, bool enabled, int? tzOffsetMinutes, string? languageCode, bool notify, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return null;
        var beta = await BattleTrackerUseCase.BetaAsync(settings, ct);
        var available = BattleTrackerUseCase.Allowed(beta, tg);

        var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
        if (tzOffsetMinutes is int tz && Math.Abs(tz) <= 14 * 60) prefs.TzOffsetMinutes = tz;
        prefs.Lang ??= TiltMessages.Code(BotText.ParseLang(languageCode));

        if (!enabled)
        {
            if (prefs.TrackerEnabled) await sentLog.AddAsync(OffKind, $"{tg}|{DateTime.UtcNow:O}", ct);
            prefs.TrackerEnabled = false;
            // Открытая карточка всё равно получит итог - опрос ведёт её до конца захода.
            await alertPrefs.SaveChangesAsync(ct);
            return ToState(prefs, available);
        }
        if (!available) return ToState(prefs, false);

        var wasEnabled = prefs.TrackerEnabled;
        var now = DateTime.UtcNow;
        var recent = await battles.GetSinceAsync(player.PlayerTag, now.AddHours(-6), ct);
        var newest = recent.Count > 0 ? recent[^1].BattleTimeUtc : now;
        // Всё уже сыгранное - история: карточка начнётся со следующего боя.
        prefs.TrackerWatermarkUtc = newest > now ? newest : now;
        prefs.TrackerEnabled = true;
        // Включает сам, в личке или после запроса права писать - значит, писать можно.
        prefs.DmBlocked = false;
        await alertPrefs.SaveChangesAsync(ct);

        if (notify && !wasEnabled)
        {
            var t = BotText.For(await TiltMessages.LangAsync(prefs, player, clans, ct));
            var sent = await sender.SendDmAsync(tg, t.TrkOn, [], silent: true, ct);
            if (sent.Blocked)
            {
                prefs.DmBlocked = true;
                await alertPrefs.SaveChangesAsync(ct);
            }
        }
        return ToState(prefs, true);
    }

    /// <summary>Ответ на /tracker. null - игрок не привязан.</summary>
    public async Task<Result?> StatusAsync(long tg, string? languageCode, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return null;
        var prefs = await alertPrefs.GetAsync(tg, ct);
        var t = BotText.For(prefs?.Lang ?? await TiltMessages.LangAsync(prefs, player, clans, ct));
        if (!BattleTrackerUseCase.Allowed(await BattleTrackerUseCase.BetaAsync(settings, ct), tg))
            return new Result(t.TrkUnavailable, []);
        return Status(t, prefs?.TrackerEnabled ?? false);
    }

    private static Result Status(BotText t, bool enabled) => new(
        string.Format(t.TrkStatus, enabled ? t.TrkStatusOn : t.TrkStatusOff),
        [
            [enabled ? new BotButton(t.TrkBtnOff, "trk|off") : new BotButton(t.TrkBtnOn, "trk|on")],
            [new BotButton(t.TrkBtnAll, Url: "startapp:matches")],
        ]);

    /// <summary>Кнопки trk|m, trk|on, trk|off. null - нажатие не наше.</summary>
    public async Task<Result?> HandleAsync(long tg, string data, string? languageCode, CancellationToken ct = default)
    {
        switch (data)
        {
            case "trk|m":
            {
                // Кнопка лежит в личке этого человека - настройки его по определению.
                var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
                var t = BotText.For(prefs.Lang ?? languageCode);
                var tz = prefs.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
                prefs.TrackerMutedUntilUtc = TiltMessages.LocalMidnightUtc(DateTime.UtcNow, tz).AddDays(1);
                prefs.TrackerCardMessageId = null;
                prefs.TrackerCardStartUtc = null;
                await alertPrefs.SaveChangesAsync(ct);
                await sentLog.AddAsync(MuteKind, $"{tg}|{DateTime.UtcNow:O}", ct);
                return new Result(t.TrkMuted, [[new BotButton(t.TrkBtnAll, Url: "startapp:matches")]]);
            }
            case TrackerText.TiltCallback:
            {
                // Включение Стоп-тильта из карточки трекера: те же бесплатные сигналы,
                // что и из приложения. Карточку не переписываем - она про заход.
                var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
                prefs.Lang ??= TiltMessages.Code(BotText.ParseLang(languageCode));
                prefs.TiltAlerts = true;
                prefs.DmBlocked = false;
                await alertPrefs.SaveChangesAsync(ct);
                var t = BotText.For(prefs.Lang);
                return new Result(string.Format(t.TrkTiltOnToast, prefs.FreeSignalsLeft), [], Toast: true);
            }
            case "trk|on":
            case "trk|off":
            {
                var on = data == "trk|on";
                var state = await SetAsync(tg, on, null, languageCode, notify: false, ct);
                var prefs = await alertPrefs.GetAsync(tg, ct);
                var t = BotText.For(prefs?.Lang ?? languageCode);
                if (state is null) return new Result(t.NotLinkedYet, []);
                if (!state.Available) return new Result(t.TrkUnavailable, []);
                return new Result(state.Enabled ? t.TrkOn : t.TrkOff, Status(t, state.Enabled).Rows);
            }
            default:
                return null;
        }
    }
}
