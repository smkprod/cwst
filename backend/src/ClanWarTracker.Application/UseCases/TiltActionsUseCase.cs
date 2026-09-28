using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Нажатия на кнопки «Стоп-тильта»: пауза, «играю дальше», «сегодня не писать» под
/// сигналом и «включить / не надо» под знакомством с тильт-типом.
///
/// Каждая кнопка переписывает то же сообщение: ответ появляется там, где человек
/// нажал, а не новым сообщением, которое снова пискнет посреди игры.
/// </summary>
public class TiltActionsUseCase(
    ITiltAlertRepository alerts,
    IPlayerAlertPrefsRepository alertPrefs,
    PlusAccess plus)
{
    /// <param name="Text">Новый текст сообщения (кнопки убираются).</param>
    public record Result(string Text);

    /// <returns>null - нажатие не наше или уже обработано: сообщение не трогаем.</returns>
    public async Task<Result?> HandleAsync(long telegramUserId, string data, string? languageCode, CancellationToken ct = default)
    {
        var parts = data.Split('|');
        if (parts is ["tilt", var action, var rawId] && int.TryParse(rawId, out var id))
            return await AlertActionAsync(telegramUserId, action, id, ct);
        if (parts is ["tintro", var choice])
            return await IntroActionAsync(telegramUserId, choice == "on", languageCode, ct);
        return null;
    }

    private async Task<Result?> AlertActionAsync(long tg, string action, int id, CancellationToken ct)
    {
        var alert = await alerts.GetAsync(id, ct);
        // Чужой сигнал нажать нельзя: кнопки лежат в личке, но номер в колбэке - не пропуск.
        if (alert is null || alert.TelegramUserId != tg || alert.Choice is not null) return null;

        var t = BotText.For(alert.Lang);
        var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
        var tz = prefs.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
        var now = DateTime.UtcNow;

        string line;
        switch (action)
        {
            case "p":
                var until = now + TiltWatchUseCase.PauseLength;
                prefs.PauseUntilUtc = until;
                alert.Choice = "pause";
                line = string.Format(t.TiltPausedLine, until.AddMinutes(tz).ToString("HH:mm"));
                break;
            case "g":
                alert.Choice = "go";
                line = t.TiltGoLine;
                break;
            case "m":
                // До конца местных суток: «сегодня» - это его сегодня, а не сервера.
                prefs.MutedUntilUtc = TiltMessages.LocalMidnightUtc(now, tz).AddDays(1);
                alert.Choice = "mute";
                line = t.TiltMutedLine;
                break;
            default:
                return null;
        }
        alert.ChoiceUtc = now;
        await alerts.SaveChangesAsync(ct);
        return new Result((alert.Text ?? "").TrimEnd() + "\n\n" + line);
    }

    private async Task<Result> IntroActionAsync(long tg, bool enable, string? languageCode, CancellationToken ct)
    {
        var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
        prefs.Lang ??= TiltMessages.Code(BotText.ParseLang(languageCode));
        var t = BotText.For(prefs.Lang);

        prefs.TiltAlerts = enable;
        if (!enable)
        {
            await alertPrefs.SaveChangesAsync(ct);
            return new Result(t.TiltIntroDeclined);
        }

        // Нажал «включить» в личке - значит, писать ему можно.
        prefs.DmBlocked = false;
        await alertPrefs.SaveChangesAsync(ct);

        var status = await plus.GetAsync(tg, ct);
        return new Result(status.Unlocked
            ? t.TiltIntroEnabledPlus
            : string.Format(t.TiltIntroEnabled, prefs.FreeSignalsLeft));
    }
}

/// <summary>
/// Знакомство с тильт-типом: одно сообщение, когда боёв набралось достаточно.
///
/// Не рассылка: пишем только тем, кто сам привязался в боте, один раз и небольшими
/// порциями. Тем, у кого тип «Лёд», не пишем вовсе - продавать им нечего, а честное
/// «тебе тильт не страшен» они увидят в приложении.
/// </summary>
public class TiltIntroUseCase(
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IPlayerBattleRepository battles,
    IClanRepository clans,
    PlusAccess plus,
    INotificationSender sender)
{
    public async Task<int> ExecuteAsync(int limit, CancellationToken ct = default)
    {
        var introduced = await alertPrefs.IntroducedAsync(ct);
        var now = DateTime.UtcNow;
        var sent = 0;

        var candidates = (await players.GetAllLinkedAsync(ct))
            .Where(p => p.TelegramUserId is long tg && !introduced.Contains(tg))
            .GroupBy(p => p.TelegramUserId!.Value)
            .Select(g => g.First())
            .ToList();

        foreach (var p in candidates)
        {
            if (sent >= limit) break;
            var tg = p.TelegramUserId!.Value;
            try
            {
                var history = (await battles.GetSinceAsync(p.PlayerTag, now - CollectPlayerBattlesUseCase.Keep, ct))
                    .Where(b => BattleAnalyzer.IsTiltEligible(b.Type)).ToList();
                var profile = BattleAnalyzer.Profile(history);
                if (profile.Type is null || profile.After2Percent is not double after2) continue;

                var prefs = await alertPrefs.GetOrCreateAsync(tg, ct);
                prefs.IntroSentUtc = now;

                // С Плюсом «Стоп-тильт» и так включён, со «Льдом» - продавать нечего.
                var status = await plus.GetAsync(tg, ct);
                if (status.Active || profile.Type == "ice")
                {
                    await alertPrefs.SaveChangesAsync(ct);
                    continue;
                }

                var lang = await TiltMessages.LangAsync(prefs, p, clans, ct);
                var t = BotText.For(lang);
                var text = string.Format(t.TiltIntro, TiltMessages.TypeName(t, profile.Type),
                    after2.ToString("0"), profile.BasePercent.ToString("0"), prefs.FreeSignalsLeft);
                var messageId = await sender.SendToUserWithButtonsAsync(tg, text,
                    [[new BotButton(t.TiltIntroOn, "tintro|on"), new BotButton(t.TiltIntroOff, "tintro|off")]], ct);

                if (messageId is null) prefs.DmBlocked = true;
                else sent++;
                await alertPrefs.SaveChangesAsync(ct);
            }
            catch
            {
                // Один сбой - не повод бросать остальных
            }
        }
        return sent;
    }
}

/// <summary>
/// Напоминание, что пропуск Плюса кончается: одно, за день-два до конца.
/// Разовый пропуск без автопродления иначе просто тихо заканчивается, и человек
/// узнаёт об этом, когда сигнал не пришёл.
/// </summary>
public class PlusReminderUseCase(
    IEntitlementRepository entitlements,
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IClanRepository clans,
    INotificationSender sender)
{
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ending = await entitlements.GetEndingAsync(now.AddHours(24), now.AddHours(60), ct);
        var sent = 0;

        foreach (var group in ending.GroupBy(e => e.TelegramUserId))
        {
            var tg = group.Key;
            var latest = group.MaxBy(e => e.UntilUtc)!;
            foreach (var e in group) e.ReminderSentUtc = now;

            // Уже продлил или спонсорство идёт дольше - напоминать не о чем.
            var until = await entitlements.UntilAsync(tg, Entitlement.Skus.Plus, ct);
            if (until > latest.UntilUtc.AddMinutes(1)) continue;
            var player = await players.GetByTelegramIdAsync(tg, ct);
            if (player is null) continue;
            if (player.IsSponsor(now) && player.SponsorUntilUtc > latest.UntilUtc.AddDays(1)) continue;

            var prefs = await alertPrefs.GetAsync(tg, ct);
            if (prefs is { DmBlocked: true }) continue;
            var t = BotText.For(await TiltMessages.LangAsync(prefs, player, clans, ct));
            var tz = prefs?.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
            var text = string.Format(t.PlusEnding, latest.UntilUtc.AddMinutes(tz).ToString("dd.MM HH:mm"));

            if (await sender.SendToUserWithButtonsAsync(tg, text,
                    [[new BotButton(t.PlusRenewButton, Url: "startapp:plus")]], ct) is not null)
                sent++;
        }

        await entitlements.SaveChangesAsync(ct);
        return sent;
    }
}
