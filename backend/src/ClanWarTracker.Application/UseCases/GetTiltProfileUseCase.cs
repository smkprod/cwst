using ClanWarTracker.Application.Battles;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Последняя серия поражений, если она была недавно: «сегодня в 21:40 — серия из 4, −112🏆».</summary>
public record RecentStreakDto(string AtUtc, int Length, int Trophies);

/// <summary>Сигнал в ленте: когда, какая серия, что нажал и чем кончилось.</summary>
public record TiltAlertRowDto(string SentUtc, string Kind, int LossStreak, string? Choice, int AfterWins, int AfterLosses, bool Summarized);

/// <summary>
/// Экран «🧊 Стоп-тильт».
///
/// Бесплатная часть показывает проблему в собственных цифрах игрока: тип, во что
/// тильт обошёлся за неделю, моменты, когда бот написал бы. Платная - то, что с
/// проблемой делать: живые сигналы, правила и «пауза работает».
/// </summary>
/// <param name="Type">ice / boiling / volcano; null - данных пока мало.</param>
/// <param name="PrevType">Тип неделю назад - для «🌡→🧊, остыл».</param>
/// <param name="WeekExtraLosses">«Тильт-налог»: сколько поражений за неделю лишние из-за тильта.</param>
/// <param name="LastSeries">Последние бои ладдера и Пути легенд: W, L, D - от старых к новым.</param>
/// <param name="Moments14">Сколько раз за 14 дней бот написал бы, и что было после.</param>
public record TiltProfileDto(
    int Games,
    int MinBattles,
    int After2Games,
    int MinSamples,
    string? Type,
    string? PrevType,
    double BasePercent,
    double? After2Percent,
    int WeekTiltBattles,
    double WeekExtraLosses,
    int WeekTiltTrophies,
    double PrevWeekExtraLosses,
    string LastSeries,
    int Moments14,
    int Moments14Wins,
    int Moments14Losses,
    RecentStreakDto? RecentStreak,
    bool Paywall,
    bool Unlocked,
    bool Active,
    bool Enabled,
    int FreeSignalsLeft,
    bool DmBlocked,
    int LossThreshold,
    int? DailyLossLimit,
    bool QuietHours,
    string? PauseUntil,
    int Alerts,
    double? PausedPercent,
    double? NotPausedPercent,
    List<TiltAlertRowDto> RecentAlerts);

public class GetTiltProfileUseCase(
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    IPlayerAlertPrefsRepository alertPrefs,
    ITiltAlertRepository alerts,
    CollectPlayerBattlesUseCase collect,
    PlusAccess plus)
{
    /// <returns>null - игрок не привязан.</returns>
    public async Task<TiltProfileDto?> ExecuteAsync(long telegramUserId, CancellationToken ct = default)
    {
        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (player is null) return null;

        try { await collect.SyncAsync(player.PlayerTag, ct); }
        catch { /* API игры недоступен - покажем то, что уже есть */ }

        var now = DateTime.UtcNow;
        var history = (await battles.GetSinceAsync(player.PlayerTag, now - CollectPlayerBattlesUseCase.Keep, ct))
            .Where(b => BattleAnalyzer.IsTiltEligible(b.Type)).ToList();

        var profile = BattleAnalyzer.Profile(history);
        var weekAgo = now.AddDays(-7);
        var prevProfile = BattleAnalyzer.Profile(history.Where(b => b.BattleTimeUtc < weekAgo).ToList());

        // «Тильт-налог»: бои «в тильте» за неделю × насколько в тильте играешь хуже.
        // Разницу берём из профиля за месяц - она устойчивее недельной.
        var gap = profile.After2Percent is double a2 ? Math.Max(0, profile.BasePercent - a2) / 100 : 0;
        var inTilt = BattleAnalyzer.InTilt(history);
        var weekTilt = inTilt.Where(b => b.BattleTimeUtc >= weekAgo).ToList();
        var prevWeekTilt = inTilt.Where(b => b.BattleTimeUtc >= weekAgo.AddDays(-7) && b.BattleTimeUtc < weekAgo).ToList();

        var moments = BattleAnalyzer.Moments(history, 2, now.AddDays(-14));

        var status = await plus.GetAsync(telegramUserId, ct);
        var prefs = await alertPrefs.GetAsync(telegramUserId, ct);
        var enabled = status.Unlocked ? prefs?.TiltAlerts != false : prefs?.TiltAlerts == true;

        var alertHistory = await alerts.GetSinceAsync(telegramUserId, now.AddDays(-60), ct);
        var summarized = alertHistory.Where(a => a.Kind == "streak" && a.SummaryUtc is not null).ToList();
        var paused = summarized.Where(a => a.Choice == "pause").ToList();
        var other = summarized.Where(a => a.Choice != "pause").ToList();
        int pw = paused.Sum(a => a.AfterWins), pl = paused.Sum(a => a.AfterLosses);
        int ow = other.Sum(a => a.AfterWins), ol = other.Sum(a => a.AfterLosses);
        var worksShown = summarized.Count >= 5 && pw + pl >= 3 && ow + ol >= 3;

        return new TiltProfileDto(
            Games: profile.Games,
            MinBattles: BattleAnalyzer.ProfileMinBattles,
            After2Games: profile.After2Games,
            MinSamples: BattleAnalyzer.ProfileMinSamples,
            Type: profile.Type,
            PrevType: prevProfile.Type,
            BasePercent: profile.BasePercent,
            After2Percent: profile.After2Percent,
            WeekTiltBattles: weekTilt.Count,
            WeekExtraLosses: Math.Round(weekTilt.Count * gap, 1),
            WeekTiltTrophies: weekTilt.Sum(b => b.TrophyChange ?? 0),
            PrevWeekExtraLosses: Math.Round(prevWeekTilt.Count * gap, 1),
            LastSeries: new string(history.TakeLast(12).Select(b => b.Result > 0 ? 'W' : b.Result < 0 ? 'L' : 'D').ToArray()),
            Moments14: moments.Count,
            Moments14Wins: moments.Sum(m => m.AfterWins),
            Moments14Losses: moments.Sum(m => m.AfterLosses),
            RecentStreak: RecentStreak(history, now),
            Paywall: status.Paywall,
            Unlocked: status.Unlocked,
            Active: status.Active,
            Enabled: enabled,
            FreeSignalsLeft: prefs?.FreeSignalsLeft ?? PlayerAlertPrefs.DefaultFreeSignals,
            DmBlocked: prefs?.DmBlocked ?? false,
            LossThreshold: prefs?.LossThreshold ?? 2,
            DailyLossLimit: prefs?.DailyLossLimit,
            QuietHours: prefs?.QuietHours ?? true,
            PauseUntil: prefs is { PauseUntilUtc: { } pause } && pause > now ? pause.ToString("O") : null,
            Alerts: alertHistory.Count(a => a.Kind == "streak"),
            PausedPercent: worksShown ? Math.Round(100.0 * pw / (pw + pl), 0) : null,
            NotPausedPercent: worksShown ? Math.Round(100.0 * ow / (ow + ol), 0) : null,
            RecentAlerts: alertHistory
                .OrderByDescending(a => a.SentUtc)
                .Take(10)
                .Select(a => new TiltAlertRowDto(a.SentUtc.ToString("O"), a.Kind, a.LossStreak, a.Choice,
                    a.AfterWins, a.AfterLosses, a.SummaryUtc is not null))
                .ToList());
    }

    /// <summary>
    /// Серия из трёх и больше поражений за последние два часа - для баннера «Стоп-тильт
    /// написал бы после второго». Самое честное место продать: боль ещё свежая.
    /// </summary>
    private static RecentStreakDto? RecentStreak(List<PlayerBattle> history, DateTime now)
    {
        var recent = history.Where(b => now - b.BattleTimeUtc <= TimeSpan.FromHours(2)).ToList();
        int run = 0, best = 0, trophies = 0, bestTrophies = 0;
        DateTime? at = null, bestAt = null;
        foreach (var b in recent)
        {
            if (b.Result < 0)
            {
                run++;
                trophies += b.TrophyChange ?? 0;
                at = b.BattleTimeUtc;
                if (run > best) { best = run; bestAt = at; bestTrophies = trophies; }
            }
            else if (b.Result > 0)
            {
                run = 0;
                trophies = 0;
            }
        }
        return best >= 3 && bestAt is { } when ? new RecentStreakDto(when.ToString("O"), best, bestTrophies) : null;
    }
}
