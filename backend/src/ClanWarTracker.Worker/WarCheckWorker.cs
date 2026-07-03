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
    private readonly HashSet<string> _reminderChatKeys = [];
    private readonly HashSet<string> _briefingKeys = [];

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
                await useCase.ExecuteAsync(_reminderChatKeys, stoppingToken);
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
                // Журнал военных боёв (кто/когда отыграл КВ + исход). Отдельный scope.
                using var scope = scopeFactory.CreateScope();
                var battles = scope.ServiceProvider.GetRequiredService<CaptureWarBattlesUseCase>();
                var n = await battles.ExecuteAsync(stoppingToken);
                if (n > 0) logger.LogInformation("Captured {Count} war battles", n);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "War battle capture failed");
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

            try
            {
                // Утренний брифинг лидера (Pro): личная сводка-план в начале военного дня.
                using var scope = scopeFactory.CreateScope();
                var briefing = scope.ServiceProvider.GetRequiredService<SendLeaderBriefingUseCase>();
                var sent = await briefing.ExecuteAsync(_briefingKeys, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent leader briefings to {Count} clans", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Leader briefing failed");
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
    /// «Последний звонок» за ~30 минут до конца военного дня. Время конца у каждого клана
    /// своё (глава задаёт в настройках), поэтому проверяем часто — каждые 10 минут — и
    /// SendFinalCallUseCase сам решает, для каких кланов сейчас окно «за 30 минут до конца».
    /// Дедуп по дню не даёт слать дважды.
    /// </summary>
    private async Task RunFinalCallLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SnapshotInterval); // те же 10 минут
        do
        {
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
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
