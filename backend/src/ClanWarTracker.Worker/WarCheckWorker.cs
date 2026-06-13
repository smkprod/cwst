using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Worker;

/// <summary>Каждые 30 минут проверяет все кланы: шлёт напоминания и сохраняет снапшоты войны.</summary>
public class WarCheckWorker(IServiceScopeFactory scopeFactory, ILogger<WarCheckWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private readonly HashSet<string> _reportedDays = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
