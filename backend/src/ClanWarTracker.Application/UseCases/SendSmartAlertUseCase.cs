using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Личный алерт игроку, который ещё не доиграл войну: показывает, на сколько именно
/// его бездействие роняет шанс клана на победу. Pro-фича — использует тот же прогноз,
/// что и Mini App (GetClanStatusUseCase), но адресует эффект конкретному игроку.
/// </summary>
public class SendSmartAlertUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    INotificationSender notifier,
    GetClanStatusUseCase clanStatus)
{
    /// <summary>Не чаще одного алерта на игрока за 6 часов (т.е. максимум ~1 в военный день).</summary>
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromHours(6);

    /// <summary>Минимальная просадка шанса победы, чтобы алерт был не спамом, а реальным сигналом.</summary>
    private const int MinChanceDrop = 5;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        foreach (var clan in await clans.GetAllAsync(ct))
        {
            if (clan.EffectivePlan(DateTime.UtcNow) != PlanTier.Pro) continue;

            ClanStatusDto? status;
            try { status = await clanStatus.ExecuteAsync(clan.ClanTag, ct); }
            catch { continue; } // CR API прилёг — не роняем цикл для остальных кланов
            if (status is null || status.Insights?.WinChance is null) continue;

            var ours = status.Race.FirstOrDefault(r => r.IsOurClan);
            var bestRival = status.Race.Where(r => !r.IsOurClan)
                .OrderByDescending(r => r.IsFinished ? r.Fame : r.ProjectedFame)
                .FirstOrDefault();
            if (ours is null || bestRival is null) continue;

            var rivalProj = bestRival.IsFinished ? bestRival.Fame : bestRival.ProjectedFame;
            var sigma = GetClanStatusUseCase.ComputeSigma(ours.ProjectedFame, ours.Fame, rivalProj, bestRival.Fame);
            var winChance = status.Insights.WinChance.Value;
            var now = DateTime.UtcNow;
            var t = NotificationSettings.Parse(clan.NotificationSettingsJson).Text;

            var linked = (await players.GetByClanIdAsync(clan.Id, ct))
                .Where(p => p.TelegramUserId is not null)
                .GroupBy(p => p.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var p in status.Players.Where(p => p.Status != "played"))
            {
                if (!linked.TryGetValue(p.PlayerTag, out var player)) continue;
                if (now - player.LastSmartAlertSentAt < AlertCooldown) continue;

                var lostFame = Math.Max(0, p.ProjectedWeekFame - p.Fame);
                if (lostFame <= 0) continue;

                var winChanceWithoutMe = GetClanStatusUseCase.ComputeWinChance(
                    ours.ProjectedFame - lostFame, rivalProj, sigma);
                if (winChance - winChanceWithoutMe < MinChanceDrop) continue;

                var decksLeft = 4 - p.DecksUsedToday;
                await notifier.SendToUserAsync(
                    player.TelegramUserId!.Value,
                    string.Format(t.SmartAlert, winChance, winChanceWithoutMe, decksLeft), ct);

                player.LastSmartAlertSentAt = now;
            }

            await players.SaveChangesAsync(ct);
        }
    }
}
