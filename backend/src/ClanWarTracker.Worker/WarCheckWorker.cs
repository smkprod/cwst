using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Worker;

/// <summary>Каждые 30 минут проверяет все кланы: шлёт напоминания и сохраняет снапшоты войны.</summary>
public class WarCheckWorker(IServiceScopeFactory scopeFactory, ILogger<WarCheckWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private readonly HashSet<string> _reportedDays = [];
    private readonly HashSet<string> _finalCallKeys = [];

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(RunPeriodicChecksAsync(stoppingToken), RunFinalCallLoopAsync(stoppingToken));

    private async Task RunPeriodicChecksAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var useCase = scope.ServiceProvider.GetRequiredService<SendRemindersUseCase>();
                await useCase.ExecuteAsync(stoppingToken);
                logger.LogInformation("Reminder check completed at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reminder check failed");
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var report = scope.ServiceProvider.GetRequiredService<SendDailyReportUseCase>();
                var sent = await report.ExecuteAsync(_reportedDays, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent {Count} daily war reports", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily report failed");
            }

            try
            {
                // Снапшоты — отдельный scope и try: сбой истории не должен ломать напоминания
                using var scope = scopeFactory.CreateScope();
                var capture = scope.ServiceProvider.GetRequiredService<CaptureWarSnapshotsUseCase>();
                var count = await capture.ExecuteAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Captured {Count} war snapshots", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Snapshot capture failed");
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var expiry = scope.ServiceProvider.GetRequiredService<SendPlanExpiryRemindersUseCase>();
                await expiry.ExecuteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plan expiry reminder failed");
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var smartAlert = scope.ServiceProvider.GetRequiredService<SendSmartAlertUseCase>();
                await smartAlert.ExecuteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Smart alert failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// CR API не отдаёт точное время сброса дня войны — система допускает, что сброс
    /// происходит в 10:00 UTC (то же допущение, на котором стоит DayEndsAtUtc). Поэтому
    /// здесь не нужен частый поллинг: будим воркер раз в сутки, ровно в 09:59 UTC,
    /// и шлём финальное предупреждение тем, кто не успел доиграть.
    /// </summary>
    private async Task RunFinalCallLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextFinalCallUtc() - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var finalCall = scope.ServiceProvider.GetRequiredService<SendFinalCallUseCase>();
                var sent = await finalCall.ExecuteAsync(_finalCallKeys, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent {Count} final-call alerts", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Final call alert failed");
            }

            // Небольшой запас, чтобы при дрифте таймера не сработать дважды на одну минуту
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private static DateTime NextFinalCallUtc()
    {
        var now = DateTime.UtcNow;
        var target = new DateTime(now.Year, now.Month, now.Day, 9, 59, 0, DateTimeKind.Utc);
        return now < target ? target : target.AddDays(1);
    }
}
