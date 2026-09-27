using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// «Стоп-тильт»: пишет игроку с Плюсом, когда он прямо сейчас сливает серию.
///
/// Это главное, за что платят: ни игра, ни сайты со статистикой не напишут посреди
/// захода «ты проиграл два подряд, а после двух подряд ты обычно выигрываешь
/// треть - сделай паузу». Для этого журнал таких игроков читается каждые десять
/// минут, а не раз в три часа, как у остальных.
///
/// Пишем только тем, кто этого хочет: у кого Плюс (или спонсорство) и кто не
/// выключил оповещения, - и не чаще раза за заход и трёх раз в сутки.
/// </summary>
public class TiltWatchUseCase(
    IEntitlementRepository entitlements,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IPlayerBattleRepository battles,
    IClanRepository clans,
    CollectPlayerBattlesUseCase collect,
    IServiceSettingRepository settings,
    INotificationSender sender)
{
    /// <summary>Последний бой не старше этого - значит, человек ещё в игре.</summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(20);

    public const int MaxAlertsPerDay = 3;

    /// <summary>Сколько наблюдений «после двух поражений» нужно, чтобы назвать личный процент.</summary>
    private const int MinTiltSamples = 8;

    public record Summary(int Watched, int Alerts, int Undelivered);

    public async Task<Summary> ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var offer = await PlusSales.ReadAsync(settings, ct);

        // Кому: купившие или получившие Плюс, спонсоры (им Плюс входит в спонсорство),
        // а когда платное выключено - ещё и все, кто явно включил оповещения сам.
        var watch = new HashSet<long>(await entitlements.ActiveUsersAsync(Entitlement.Skus.Plus, now, ct));
        foreach (var p in await players.GetAllLinkedAsync(ct))
            if (p.TelegramUserId is long tg && p.IsSponsor(now)) watch.Add(tg);
        if (!offer.Paywall) watch.UnionWith(await alertPrefs.OptedInAsync(ct));

        int alerts = 0, undelivered = 0;
        foreach (var tg in watch)
        {
            try
            {
                var sent = await CheckOneAsync(tg, now, ct);
                if (sent == true) alerts++;
                else if (sent == false) undelivered++;
            }
            catch
            {
                // Один сломанный журнал не должен лишать оповещений остальных
            }
        }
        return new Summary(watch.Count, alerts, undelivered);
    }

    /// <returns>true - написали, false - написать не удалось, null - писать было незачем.</returns>
    private async Task<bool?> CheckOneAsync(long tg, DateTime now, CancellationToken ct)
    {
        var prefs = await alertPrefs.GetAsync(tg, ct);
        if (prefs is { TiltAlerts: false } or { DmBlocked: true }) return null;

        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (player is null) return null;

        try { await collect.SyncAsync(player.PlayerTag, ct); }
        catch { /* API игры недоступен - посмотрим на то, что уже есть */ }

        var recent = await battles.GetSinceAsync(player.PlayerTag, now.AddHours(-3), ct);
        var check = BattleAnalyzer.ShouldAlertTilt(recent, now, prefs?.LastAlertBattleUtc, Freshness);
        if (!check.Alert) return null;

        var day = now.ToString("yyyy-MM-dd");
        if (prefs is not null && prefs.AlertDay == day && prefs.AlertsToday >= MaxAlertsPerDay) return null;

        var history = await battles.GetSinceAsync(player.PlayerTag, now - CollectPlayerBattlesUseCase.Keep, ct);
        var t = await TextAsync(player, ct);
        var text = Compose(t, check.LossStreak, history);

        var delivered = await sender.TrySendToUserAsync(tg, text, ct);

        prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
        // Запоминаем и неудачную попытку: про эту серию больше не пытаемся.
        prefs.LastAlertBattleUtc = check.LastBattleUtc;
        if (delivered)
        {
            prefs.LastAlertSentUtc = now;
            prefs.AlertsToday = prefs.AlertDay == day ? prefs.AlertsToday + 1 : 1;
            prefs.AlertDay = day;
        }
        else
        {
            prefs.DmBlocked = true;
        }
        await alertPrefs.SaveChangesAsync(ct);
        return delivered;
    }

    /// <summary>
    /// Текст с личной цифрой, когда её уже можно назвать честно, и общий - пока рано.
    /// Личная цифра убеждает сильнее любого совета: это его собственные бои.
    /// </summary>
    public static string Compose(BotText t, int lossStreak, IReadOnlyList<PlayerBattle> history)
    {
        var tilt = BattleAnalyzer.Tilt(history);
        var totals = BattleAnalyzer.Count(history);
        var overall = BattleAnalyzer.Percent(totals.Wins, totals.Games);
        var after2 = BattleAnalyzer.Percent(tilt.AfterTwoLossesWins, tilt.AfterTwoLosses);

        var body = tilt.AfterTwoLosses >= MinTiltSamples && after2 < overall
            ? string.Format(t.TiltAlertStats, after2.ToString("0"), overall.ToString("0"))
            : t.TiltAlertGeneric;

        return string.Format(t.TiltAlertHead, lossStreak) + "\n\n" + body + "\n\n" + t.TiltAlertTail;
    }

    /// <summary>Язык - клана игрока, если он есть; иначе русский, как у остальных личных сообщений.</summary>
    private async Task<BotText> TextAsync(Player player, CancellationToken ct)
    {
        try
        {
            if (player.ClanId is int clanId && await clans.GetByIdAsync(clanId, ct) is { } clan)
                return NotificationSettings.Parse(clan.NotificationSettingsJson).Text;
        }
        catch { /* язык - не повод промолчать */ }
        return BotText.Ru;
    }
}
