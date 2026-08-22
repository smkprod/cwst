using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;

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
    /// <summary>
    /// Наборы ключей «это уже отправлено». Живут в памяти ради скорости, но дублируются
    /// в БД: рестарт воркера не должен приводить к повторной рассылке. Именно из-за
    /// потери этих наборов при деплое бот заново поздравлял всех, кто уже набрал 900,
    /// — условие «набрал 900» истинно до конца военного дня, так что срабатывало снова.
    /// </summary>
    private readonly HashSet<string> _reportedDays = [];
    private readonly HashSet<string> _finalCallKeys = [];
    private readonly HashSet<string> _reminderChatKeys = [];
    private readonly HashSet<string> _briefingKeys = [];
    private readonly HashSet<string> _perfectDayKeys = [];
    private readonly HashSet<string> _respectDigestKeys = [];

    private const string KindReport = "dailyreport";
    private const string KindFinalCall = "finalcall";
    private const string KindReminder = "reminder";
    private const string KindBriefing = "briefing";
    private const string KindPerfectDay = "perfectday";
    private const string KindRespectDigest = "respectdigest";

    /// <summary>
    /// Как далеко в прошлое поднимаем отметки при старте. Все наши уведомления привязаны
    /// к военной неделе, так что двух недель хватает с запасом; хранить больше незачем.
    /// </summary>
    private static readonly TimeSpan KeyHistory = TimeSpan.FromDays(14);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RestoreSentKeysAsync(stoppingToken);

        await Task.WhenAll(
            RunPeriodicChecksAsync(stoppingToken),
            RunSnapshotLoopAsync(stoppingToken),
            RunFinalCallLoopAsync(stoppingToken));
    }

    /// <summary>Поднимает отметки об уже отправленном из БД и подчищает совсем старые.</summary>
    private async Task RestoreSentKeysAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var log = scope.ServiceProvider.GetRequiredService<ISentNotificationRepository>();
            var since = DateTime.UtcNow - KeyHistory;

            foreach (var (kind, set) in Sets())
            {
                var stored = await log.GetKeysAsync(kind, since, ct);
                set.UnionWith(stored);
                // Помечаем поднятое как уже сохранённое, иначе первый же тик попробует
                // записать всё заново и словит нарушение уникального индекса на каждом ключе.
                _persisted[kind] = stored;
            }

            await log.PurgeOlderThanAsync(since, ct);

            var total = Sets().Sum(x => x.Set.Count);
            logger.LogInformation("Restored {Count} sent-notification keys", total);
        }
        catch (Exception ex)
        {
            // Не подняли отметки — худшее, что случится, это повтор одного сообщения.
            // Останавливать из-за этого весь воркер куда хуже.
            logger.LogError(ex, "Could not restore sent-notification keys");
        }
    }

    private (string Kind, HashSet<string> Set)[] Sets() =>
    [
        (KindReport, _reportedDays),
        (KindFinalCall, _finalCallKeys),
        (KindReminder, _reminderChatKeys),
        (KindBriefing, _briefingKeys),
        (KindPerfectDay, _perfectDayKeys),
        (KindRespectDigest, _respectDigestKeys),
    ];

    /// <summary>
    /// Записывает в БД ключи, добавленные use case'ом за этот проход. Сравниваем с тем,
    /// что уже сохранено, — так use case'ы остаются в неведении про хранилище.
    /// </summary>
    private async Task PersistKeysAsync(string kind, HashSet<string> set, CancellationToken ct)
    {
        if (!_persisted.TryGetValue(kind, out var known))
            _persisted[kind] = known = [];

        var fresh = set.Where(k => !known.Contains(k)).ToList();
        if (fresh.Count == 0) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var log = scope.ServiceProvider.GetRequiredService<ISentNotificationRepository>();
            foreach (var key in fresh)
            {
                await log.AddAsync(kind, key, ct);
                known.Add(key);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not persist sent-notification keys for {Kind}", kind);
        }
    }

    /// <summary>Что уже лежит в БД — чтобы не переписывать одно и то же каждый тик.</summary>
    private readonly Dictionary<string, HashSet<string>> _persisted = [];

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
                await PersistKeysAsync(KindReminder, _reminderChatKeys, stoppingToken);
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
                await PersistKeysAsync(KindReport, _reportedDays, stoppingToken);
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
                var sent = await warStart.ExecuteAsync(stoppingToken);
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
                await PersistKeysAsync(KindBriefing, _briefingKeys, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent leader briefings to {Count} clans", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Leader briefing failed");
            }

            try
            {
                // Вечерний топ респектов дня в чат клана.
                using var scope = scopeFactory.CreateScope();
                var digest = scope.ServiceProvider.GetRequiredService<SendRespectDigestUseCase>();
                var sent = await digest.ExecuteAsync(_respectDigestKeys, stoppingToken);
                await PersistKeysAsync(KindRespectDigest, _respectDigestKeys, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent {Count} respect digests", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Respect digest failed");
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

            try
            {
                // Поздравление «900 за день» — в частом цикле, чтобы прилетало в чат
                // почти сразу после четвёртой победы, пока эмоция горячая.
                using var scope = scopeFactory.CreateScope();
                var perfectDay = scope.ServiceProvider.GetRequiredService<SendPerfectDayUseCase>();
                var sent = await perfectDay.ExecuteAsync(_perfectDayKeys, stoppingToken);
                await PersistKeysAsync(KindPerfectDay, _perfectDayKeys, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent {Count} perfect-day congrats", sent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Perfect day congrats failed");
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
                await PersistKeysAsync(KindFinalCall, _finalCallKeys, stoppingToken);
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
