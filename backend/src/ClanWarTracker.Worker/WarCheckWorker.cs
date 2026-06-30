using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Worker;

/// <summary>
/// Фоновый воркер: каждые 30 минут — напоминания/отчёты, отдельно каждые 10 минут —
/// снапшоты войны (чаще = точнее дневные дельты медалей игроков), и раз в сутки —
/// «последний звонок» перед концом дня.
/// </summary>
public class WarCheckWorker(IServiceScopeFactory scopeFactory, ILogger<WarCheckWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    // Снапшоты снимаем чаще основного цикла: финальный снимок дня всегда свежий (в пределах
    // 10 минут до сброса в 10:00 UTC), поэтому «медали за день» по каждому игроку точнее.
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(10);
    private readonly HashSet<string> _reportedDays = [];
    private readonly HashSet<string> _finalCallKeys = [];
    private readonly HashSet<string> _warStartKeys = [];

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            RunPeriodicChecksAsync(stoppingToken),
            RunSnapshotLoopAsync(stoppingToken),
            RunFinalCallLoopAsync(stoppingToken));

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
                using var scope = scopeFactory.CreateScope();
                var warStart = scope.ServiceProvider.GetRequiredService<SendWarStartUseCase>();
                var sent = await warStart.ExecuteAsync(_warStartKeys, stoppingToken);
                if (sent > 0) logger.LogInformation("Announced war start to {Count} clans", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "War start announcement failed");
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
    /// Отдельный частый цикл снапшотов (каждые 10 минут). Снимок идемпотентен (upsert по
    /// ключу дня), поэтому «снимок текущего дня» постоянно обновляется свежими данными —
    /// к моменту сброса дня он отстаёт не более чем на 10 минут, и дневные дельты медалей
    /// по каждому игроку считаются точнее. GetCurrentWarAsync кэшируется на 2 минуты, так
    /// что лишней нагрузки на CR API почти нет.
    /// </summary>
    private async Task RunSnapshotLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SnapshotInterval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var capture = scope.ServiceProvider.GetRequiredService<CaptureWarSnapshotsUseCase>();
                var count = await capture.ExecuteAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Captured {Count} war snapshots", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Snapshot capture failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// CR API не отдаёт точное время сброса дня войны — система допускает, что сброс
    /// происходит в 10:00 UTC (то же допущение, на котором стоит DayEndsAtUtc). Поэтому
    /// здесь не нужен частый поллинг: будим воркер раз в сутки в 09:30 UTC (за ~30 минут
    /// до конца дня — за это время реально успеть доиграть), и шлём последний звонок тем,
    /// кто не успел доиграть.
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
        var target = new DateTime(now.Year, now.Month, now.Day, 9, 30, 0, DateTimeKind.Utc);
        return now < target ? target : target.AddDays(1);
    }
}
