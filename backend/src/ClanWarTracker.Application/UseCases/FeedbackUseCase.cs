using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Опрос «что добавить»: какие функции нужны и почему купил (или что мешает купить).
/// Отвечают в приложении; бот зовёт туда лично - тех, кто платил, и самых активных.
///
/// Не рассылка: один раз на человека, только тем, кто сам открыл личку с ботом, не
/// больше <see cref="AsksPerDay"/> в день и только днём. За первый ответ - пара дней
/// Плюса: так отвечают охотнее, а заодно пробуют платное.
/// </summary>
public class FeedbackUseCase(
    IFeedbackRepository answers,
    IPlayerRepository players,
    ISponsorPaymentRepository payments,
    IActivityRepository activity,
    IPlayerAlertPrefsRepository alertPrefs,
    ISentNotificationRepository sentLog,
    INotificationSender notifier,
    PlusAccess plus)
{
    /// <summary>Что добавить - варианты ответа (несколько сразу).</summary>
    public static readonly string[] Wants =
        ["deep_review", "deck_for_me", "war_scout", "share_cards", "daily_goals", "tournaments", "leader_tools", "rivals"];

    /// <summary>За что купил - тем, кто платил.</summary>
    public static readonly string[] BuyerReasons = ["tilt", "review", "tracker", "star", "support"];

    /// <summary>Что мешает взять Плюс - тем, кто не платил.</summary>
    public static readonly string[] PlayerReasons = ["price", "unclear", "no_stars", "not_needed", "didnt_see"];

    public const int RewardPlusDays = 2;
    public const int AsksPerDay = 15;
    public const int MaxTextLength = 500;

    /// <summary>Активный - заходил хотя бы столько дней из последних двух недель.</summary>
    public const int ActiveDays = 3;

    private const string AskedKind = SentNotification.OncePrefix + "fbask";
    private const string DayKind = "fbask.day";
    private const int TzOffsetMinutes = 180;

    public record Mine(bool Answered, bool IsBuyer, int RewardPlusDays);

    public async Task<Mine> MineAsync(long tg, CancellationToken ct = default) =>
        new(await answers.GetAsync(tg, ct) is not null, await IsBuyerAsync(tg, ct), RewardPlusDays);

    public enum SubmitOutcome { Ok, Invalid }

    /// <returns>Ok и сколько дней Плюса начислено (0 - ответ не первый).</returns>
    public async Task<(SubmitOutcome Outcome, int Granted)> SubmitAsync(
        long tg, IReadOnlyList<string>? wants, string? reason, string? text, CancellationToken ct = default)
    {
        var buyer = await IsBuyerAsync(tg, ct);
        var picked = (wants ?? []).Where(w => Wants.Contains(w)).Distinct().ToList();
        var reasons = buyer ? BuyerReasons : PlayerReasons;
        var why = reason is not null && reasons.Contains(reason) ? reason : null;
        var note = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (note is { Length: > MaxTextLength }) note = note[..MaxTextLength];
        // Пустой ответ - не ответ: за него не дарим Плюс
        if (picked.Count == 0 && why is null && note is null) return (SubmitOutcome.Invalid, 0);

        var now = DateTime.UtcNow;
        var player = await players.GetByTelegramIdAsync(tg, ct);
        var existing = await answers.GetAsync(tg, ct);
        if (existing is not null)
        {
            existing.Segment = buyer ? "buyer" : "player";
            existing.Wants = string.Join(',', picked);
            existing.Reason = why;
            existing.Text = note;
            existing.PlayerTag = player?.PlayerTag ?? existing.PlayerTag;
            existing.UpdatedUtc = now;
            await answers.SaveChangesAsync(ct);
            return (SubmitOutcome.Ok, 0);
        }

        await answers.AddAsync(new FeedbackAnswer
        {
            TelegramUserId = tg,
            PlayerTag = player?.PlayerTag,
            Segment = buyer ? "buyer" : "player",
            Wants = string.Join(',', picked),
            Reason = why,
            Text = note,
            CreatedUtc = now,
            UpdatedUtc = now,
        }, ct);
        // Награда - только привязанному: Плюс без профиля бесполезен
        if (player is null) return (SubmitOutcome.Ok, 0);
        await plus.GrantAsync(tg, RewardPlusDays, Entitlement.Sources.Grant, player.PlayerTag, 0, null, ct);
        return (SubmitOutcome.Ok, RewardPlusDays);
    }

    private async Task<bool> IsBuyerAsync(long tg, CancellationToken ct) =>
        (await payments.GetAllAsync(ct)).Any(p => p.PayerTelegramUserId == tg && p.RefundedAtUtc is null);

    /// <summary>
    /// Проход воркера: позвать в опрос - сначала тех, кто заплатил (через сутки после
    /// оплаты), потом самых активных. Возвращает, скольким написали.
    /// </summary>
    public async Task<int> AskTickAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var local = now.AddMinutes(TzOffsetMinutes);
        if (local.Hour is < 12 or >= 20) return 0;

        var today = local.ToString("yyyy-MM-dd");
        var sentToday = (await sentLog.GetKeysAsync(DayKind, now.AddDays(-1), ct)).Count(k => k.StartsWith(today + ":", StringComparison.Ordinal));
        if (sentToday >= AsksPerDay) return 0;

        var asked = await sentLog.GetKeysAsync(AskedKind, DateTime.MinValue, ct);
        var answered = await answers.AnsweredIdsAsync(ct);

        // Покупатели: заплатил за последние две недели и прошли сутки - успел попользоваться
        var buyers = (await payments.GetAllAsync(ct))
            .Where(p => p.RefundedAtUtc is null && p.PaidAtUtc <= now.AddHours(-20) && p.PaidAtUtc >= now.AddDays(-14))
            .Select(p => p.PayerTelegramUserId)
            .Distinct()
            .ToList();

        // Активные: заходили хотя бы ActiveDays дней из последних 14, самые частые первыми
        var since = DateOnly.FromDateTime(now).AddDays(-13).ToString("yyyy-MM-dd");
        var linked = (await players.GetAllLinkedAsync(ct)).Where(p => p.TelegramUserId is not null).ToList();
        var tgByPlayer = linked.ToDictionary(p => p.Id, p => p.TelegramUserId!.Value);
        var actives = (await activity.GetAllDaysAsync(ct))
            .Where(d => string.CompareOrdinal(d.DayUtc, since) >= 0 && tgByPlayer.ContainsKey(d.PlayerId))
            .GroupBy(d => tgByPlayer[d.PlayerId])
            .Select(g => (Tg: g.Key, Days: g.Select(d => d.DayUtc).Distinct().Count()))
            .Where(x => x.Days >= ActiveDays)
            .OrderByDescending(x => x.Days)
            .Select(x => x.Tg)
            .ToList();

        var queue = buyers.Select(tg => (Tg: tg, Buyer: true))
            .Concat(actives.Where(a => !buyers.Contains(a)).Select(tg => (Tg: tg, Buyer: false)));

        var sent = 0;
        foreach (var (tg, buyer) in queue)
        {
            if (sentToday + sent >= AsksPerDay) break;
            var key = tg.ToString();
            if (asked.Contains(key) || answered.Contains(tg)) continue;
            var prefs = await alertPrefs.GetAsync(tg, ct);
            if (prefs is { DmBlocked: true }) continue;
            if (!await sentLog.TryClaimAsync(AskedKind, key, ct)) continue;
            asked.Add(key);

            var t = FeedbackText.For(prefs?.Lang);
            var result = await notifier.SendDmAsync(tg, string.Format(buyer ? t.Buyer : t.Active, RewardPlusDays),
                [[new BotButton(t.Button, Url: "startapp:feedback")]], silent: false, ct);
            await sentLog.AddAsync(DayKind, $"{today}:{key}", ct);
            if (result.Delivered) sent++;
            else if (result.Blocked && prefs is not null)
            {
                prefs.DmBlocked = true;
                await alertPrefs.SaveChangesAsync(ct);
            }
        }
        return sent;
    }

    public record CountRow(string Key, int Count);

    public record AnswerRow(string Segment, string? PlayerTag, string Wants, string? Reason, string? Text, DateTime UpdatedUtc);

    /// <summary>Сводка для панели: что просят, за что платят, что мешает, и свежие ответы.</summary>
    public record Summary(int Total, int Buyers, List<CountRow> Wants, List<CountRow> BuyerReasons,
        List<CountRow> PlayerReasons, List<AnswerRow> Recent);

    public async Task<Summary> SummaryAsync(CancellationToken ct = default)
    {
        var all = await answers.GetAllAsync(ct);
        List<CountRow> Count(IEnumerable<string> keys, IEnumerable<string> order) =>
            order.Select(k => new CountRow(k, keys.Count(x => x == k))).Where(r => r.Count > 0)
                .OrderByDescending(r => r.Count).ToList();
        return new Summary(
            all.Count,
            all.Count(a => a.Segment == "buyer"),
            Count(all.SelectMany(a => a.Wants.Split(',', StringSplitOptions.RemoveEmptyEntries)), Wants),
            Count(all.Where(a => a.Segment == "buyer" && a.Reason is not null).Select(a => a.Reason!), BuyerReasons),
            Count(all.Where(a => a.Segment != "buyer" && a.Reason is not null).Select(a => a.Reason!), PlayerReasons),
            all.Take(40).Select(a => new AnswerRow(a.Segment, a.PlayerTag, a.Wants, a.Reason, a.Text, a.UpdatedUtc)).ToList());
    }
}
