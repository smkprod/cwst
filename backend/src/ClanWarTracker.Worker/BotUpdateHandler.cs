using System.Collections.Concurrent;
using System.Text;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ClanWarTracker.Worker;

/// <summary>Long polling + команды /setup, /link, /status, /start.</summary>
public class BotUpdateHandler(
    ITelegramBotClient bot,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<BotUpdateHandler> logger) : BackgroundService
{
    private string _botUsername = "bot";

    /// <summary>Ожидающие рефералы: TG ID нового пользователя → TG ID пригласившего.
    /// Заполняется при /start ref_&lt;id&gt; и расходуется при первой привязке тега.
    /// In-memory: при перезапуске воркера незавершённые рефералы теряются — это допустимо.</summary>
    private readonly ConcurrentDictionary<long, long> _pendingReferrals = new();

    /// <summary>
    /// Сколько сообщений обрабатываем одновременно. Telegram.Bot ждёт завершения
    /// обработчика, прежде чем взять следующее обновление, поэтому одна медленная
    /// команда задерживала ВСЕ сообщения во всех чатах — а серия /bind подряд
    /// складывалась в заметное подвисание. Обрабатываем параллельно, но не
    /// бесконтрольно: и CR API, и Bot API одинаково не любят внезапный шквал.
    /// </summary>
    private readonly SemaphoreSlim _handling = new(6);

    /// <summary>Кто админ в чате: живой вызов Bot API, а команда проверяет это каждый раз.</summary>
    private readonly ConcurrentDictionary<(long Chat, long User), (bool IsAdmin, DateTime Until)> _adminCache = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var me = await bot.GetMe(stoppingToken);
            _botUsername = me.Username ?? "bot";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch bot username");
        }

        bot.StartReceiving(
            HandleUpdateAsync,
            (_, ex, _) => { logger.LogError(ex, "Bot polling error"); return Task.CompletedTask; },
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
            stoppingToken);

        logger.LogInformation("Bot polling started as @{Username}", _botUsername);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Точка входа long polling. Саму обработку уводим в отдельную задачу: пока
    /// обработчик не вернётся, Telegram.Bot не заберёт следующее обновление, и
    /// одно медленное сообщение тормозит очередь целиком.
    ///
    /// Побочный эффект — сообщения одного чата могут обработаться не по порядку.
    /// Для наших команд это безразлично: каждая самостоятельна, а ответ бот шлёт
    /// реплаем на свою команду, так что в чате всё остаётся на своих местах.
    /// </summary>
    private Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: not null }) return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                await _handling.WaitAsync(ct);
                try { await ProcessMessageAsync(update, ct); }
                finally { _handling.Release(); }
            }
            catch (OperationCanceledException) { /* воркер останавливается */ }
            catch (Exception ex) { logger.LogError(ex, "Update processing failed"); }
        });

        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } msg) return;

        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            // Любое сообщение — повод освежить @username: он нужен, чтобы тегать человека
            // в чате, а меняться может в любой момент (и раньше писался только при /link).
            await RefreshUsernameAsync(msg, sp, ct);

            // Быстрый поиск по тегу: пользователь просто отправляет #ТЕГ без команды
            if (msg.Chat.Type == ChatType.Private && !text.StartsWith('/') && IsLikelyCrTag(text))
            {
                await HandleQuickLookupAsync(msg, text, sp, ct);
                return;
            }

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].Split('@')[0]; // "/link@MyBot" -> "/link"
            var arg = parts.Length > 1 ? parts[1] : null;
            var t = await TextForAsync(msg, sp, ct);

            switch (command)
            {
                case "/start":
                    if (msg.Chat.Type == ChatType.Private)
                    {
                        // Реферальная ссылка: /start ref_<telegramId> — запоминаем пригласившего
                        // до момента, когда новый пользователь пришлёт свой тег.
                        if (arg is not null && arg.StartsWith("ref_", StringComparison.Ordinal)
                            && long.TryParse(arg.AsSpan(4), out var refUserId) && refUserId != msg.From!.Id)
                        {
                            _pendingReferrals[msg.From!.Id] = refUserId;
                        }

                        await bot.SendMessage(msg.Chat.Id, t.StartPrivate, cancellationToken: ct);
                    }
                    else
                    {
                        var groupClanRepo = sp.GetRequiredService<IClanRepository>();
                        var groupClan = await groupClanRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                        await bot.SendMessage(msg.Chat.Id,
                            groupClan is null
                                ? t.StartGroupNew
                                : string.Format(t.StartGroupReady, groupClan.Name),
                            messageThreadId: msg.MessageThreadId,
                            cancellationToken: ct);
                    }
                    break;

                case "/setup":
                    if (msg.Chat.Type == ChatType.Private)
                    {
                        await Reply(msg, t.OnlyInGroup, ct);
                        return;
                    }
                    if (arg is null) { await Reply(msg, t.SetupFormat, ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, t.SetupOnlyAdmin, ct); return; }
                    var clanName = await sp.GetRequiredService<SetupClanUseCase>()
                        .ExecuteAsync(msg.Chat.Id, arg, msg.MessageThreadId, ct);
                    var topicNote = msg.MessageThreadId is not null ? t.SetupTopicNote : "";
                    await Reply(msg, clanName is null
                        ? t.SetupClanNotFound
                        : string.Format(t.SetupOk, clanName) + topicNote, ct);
                    break;

                case "/link":
                    if (arg is null) { await Reply(msg, t.LinkFormat, ct); return; }
                    var isPrivate = msg.Chat.Type == ChatType.Private;
                    var linkChatId = isPrivate ? (long?)null : msg.Chat.Id;
                    var linkReferrer = _pendingReferrals.TryRemove(msg.From!.Id, out var lr) ? lr : (long?)null;
                    var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
                        .ExecuteAsync(msg.From!.Id, arg, linkChatId, linkReferrer, msg.From!.Username, ct);
                    await Reply(msg, playerName is null
                        ? t.LinkNotFound
                        : string.Format(isPrivate ? t.LinkOkPrivate : t.LinkOkGroup, playerName), ct);
                    break;

                case "/remind":
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, t.RemindOnlyAdmin, ct); return; }
                    if (!int.TryParse(arg, out var hours) || hours is < 1 or > 12)
                    {
                        await Reply(msg, t.RemindFormat, ct);
                        return;
                    }
                    var clanRepo = sp.GetRequiredService<IClanRepository>();
                    var remindClan = await clanRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (remindClan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }
                    remindClan.ReminderHoursBeforeEnd = hours;
                    await clanRepo.SaveChangesAsync(ct);
                    await Reply(msg, string.Format(t.RemindOk, hours), ct);
                    break;

                case "/settopic":
                case "/topic":
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, t.OnlyInGroup, ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, t.TopicOnlyAdmin, ct); return; }
                    var topicRepo = sp.GetRequiredService<IClanRepository>();
                    var topicClan = await topicRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (topicClan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }
                    topicClan.TelegramMessageThreadId = msg.MessageThreadId;
                    await topicRepo.SaveChangesAsync(ct);
                    await Reply(msg, msg.MessageThreadId is not null ? t.TopicSetToThread : t.TopicSetToChat, ct);
                    break;

                case "/nudge":
                case "/пни":
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, t.OnlyInGroup, ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, t.NudgeOnlyAdmin, ct); return; }
                    var nudgeRepo = sp.GetRequiredService<IClanRepository>();
                    var nudgeClan = await nudgeRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (nudgeClan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }
                    var isProNudge = nudgeClan.EffectivePlan(DateTime.UtcNow) == PlanTier.Pro;
                    var nudgeResult = await sp.GetRequiredService<NudgePlayersUseCase>()
                        .ExecuteAsync(nudgeClan.Id, isProNudge, ct);
                    if (nudgeResult is null) { await Reply(msg, t.NudgeNoWarDay, ct); return; }
                    if (nudgeResult.TaggableCount == 0 && nudgeResult.UnlinkedCount == 0)
                        await Reply(msg, t.NudgeAllPlayed, ct);
                    else if (nudgeResult.TaggableCount == 0)
                        await Reply(msg, string.Format(t.NudgeNobodyTaggable, nudgeResult.UnlinkedCount), ct);
                    break;

                case "/bind":
                case "/привязать":
                case "/прив'язати":
                {
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, t.OnlyInGroup, ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, t.BindOnlyAdmin, ct); return; }

                    var bindRepo = sp.GetRequiredService<IClanRepository>();
                    var bindClan = await bindRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (bindClan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }

                    if (arg is null)
                    {
                        await Reply(msg, t.BindHelp, ct);
                        return;
                    }

                    // Ответ на сообщение даёт и ID, и юзернейм: ID переживает смену ника.
                    // НО: в форум-теме Telegram кладёт в ReplyToMessage служебное сообщение
                    // о создании темы — это корень треда, а не ответ человеку. Без этой
                    // проверки все привязки уезжали на автора темы (обычно на самого лидера).
                    var replyFrom = RealReplyAuthor(msg);

                    var typedUsername = parts.Length > 2 ? parts[2].TrimStart('@').Trim() : null;
                    if (string.IsNullOrWhiteSpace(typedUsername)) typedUsername = null;

                    // «Максим» — это имя, а не юзернейм. Сохранив его, бот потом тегал бы
                    // несуществующего @Максим, и лидер узнал бы об этом только в бою.
                    if (typedUsername is not null && !IsTelegramUsername(typedUsername))
                    {
                        await Reply(msg, string.Format(t.BindBadUsername, typedUsername), ct);
                        return;
                    }

                    string? bindUsername;
                    long? bindUserId;
                    if (typedUsername is not null)
                    {
                        // Лидер назвал человека прямо — это и есть его намерение.
                        // ID из ответа берём, только если ответ про того же человека.
                        bindUsername = typedUsername;
                        bindUserId = string.Equals(replyFrom?.Username, typedUsername, StringComparison.OrdinalIgnoreCase)
                            ? replyFrom?.Id
                            : null;
                    }
                    else
                    {
                        bindUsername = replyFrom?.Username;
                        bindUserId = replyFrom?.Id;
                    }

                    if (string.IsNullOrWhiteSpace(bindUsername) && bindUserId is null)
                    {
                        await Reply(msg, t.BindWho, ct);
                        return;
                    }

                    var bindResult = await sp.GetRequiredService<BindPlayerUseCase>()
                        .BindAsync(bindClan.Id, arg, bindUsername, bindUserId, ct);

                    await Reply(msg, bindResult.Outcome switch
                    {
                        BindOutcome.TagNotFound => t.BindTagNotFound,
                        BindOutcome.NotInClan => t.BindNotInClan,
                        _ => string.Format(t.BindOk, bindResult.PlayerName,
                                 bindUsername is not null ? $"@{bindUsername}" : t.BindOkAccount) +
                             // Один Telegram-аккаунт может быть привязан только к одному тегу,
                             // поэтому перенос — это молчаливая потеря прошлой привязки. Говорим вслух.
                             (bindResult.MovedFromTag is string old ? string.Format(t.BindMoved, old) : "") +
                             (bindResult.CanDm ? "" : t.BindNoDm)
                    }, ct);
                    break;
                }

                case "/unbind":
                case "/отвязать":
                case "/відв'язати":
                {
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, t.OnlyInGroup, ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, t.UnbindOnlyAdmin, ct); return; }
                    if (arg is null) { await Reply(msg, t.UnbindNeedTag, ct); return; }

                    var unbindRepo = sp.GetRequiredService<IClanRepository>();
                    var unbindClan = await unbindRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (unbindClan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }

                    var unbindResult = await sp.GetRequiredService<BindPlayerUseCase>()
                        .UnbindAsync(unbindClan.Id, arg, ct);

                    await Reply(msg, unbindResult.Outcome == BindOutcome.Ok
                        ? string.Format(t.UnbindOk, unbindResult.PlayerName)
                        : t.UnbindNothing, ct);
                    break;
                }

                case "/unlinked":
                case "/непривязанные":
                case "/неприв'язані":
                {
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, t.OnlyInGroup, ct); return; }

                    var ulRepo = sp.GetRequiredService<IClanRepository>();
                    var ulClan = await ulRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (ulClan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }

                    var ulApi = sp.GetRequiredService<IClashRoyaleApi>();
                    var ulWar = await ulApi.GetCurrentWarAsync(ulClan.ClanTag, ct);
                    if (ulWar is null) { await Reply(msg, t.UnlinkedRosterFail, ct); return; }

                    var ulRoles = await ulApi.GetClanMemberRolesAsync(ulClan.ClanTag, ct);
                    var ulLinked = (await sp.GetRequiredService<IPlayerRepository>().GetByClanIdAsync(ulClan.Id, ct))
                        .Where(p => p.TelegramUserId is not null || !string.IsNullOrEmpty(p.TelegramUsername))
                        .Select(p => p.PlayerTag)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var ulMissing = ulWar.Participants
                        .Where(p => (ulRoles.Count == 0 || ulRoles.ContainsKey(p.PlayerTag)) && !ulLinked.Contains(p.PlayerTag))
                        .OrderBy(p => p.Name)
                        .ToList();

                    if (ulMissing.Count == 0) { await Reply(msg, t.UnlinkedAllLinked, ct); return; }

                    var ulList = string.Join("\n", ulMissing.Take(40).Select(p => $"• {p.Name} — {p.PlayerTag}"));
                    await Reply(msg, string.Format(t.UnlinkedList, ulMissing.Count, ulList), ct);
                    break;
                }

                case "/status":
                    var statusUseCase = sp.GetRequiredService<GetClanStatusUseCase>();
                    var clans = sp.GetRequiredService<IClanRepository>();
                    var clan = await clans.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (clan is null) { await Reply(msg, t.ClanNotLinked, ct); return; }

                    var status = await statusUseCase.ExecuteAsync(clan.ClanTag, ct);
                    if (status is null) { await Reply(msg, t.StatusNoWarData, ct); return; }

                    var played = status.Players.Count(p => p.Status == "played");
                    var lines = status.Players.Take(15).Select(p => p.Status switch
                    {
                        "played" => $"✅ {p.Name} ({p.DecksUsedToday}/4)",
                        "notPlayed" => $"❌ {p.Name} ({p.DecksUsedToday}/4)",
                        _ => $"⏳ {p.Name} ({p.DecksUsedToday}/4)"
                    });
                    var forecastLine = status.Forecast is null || status.PeriodType == "training"
                        ? ""
                        : string.Format(t.StatusForecast,
                              status.Forecast.ProjectedDayFame.ToString("N0"),
                              status.Forecast.ProjectedWeekFame.ToString("N0")) + "\n";
                    await Reply(msg,
                        string.Format(t.StatusHeader, status.ClanName, Period(status.PeriodType, t)) + "\n" +
                        string.Format(t.StatusPlayed, played, status.Players.Count) + "\n" +
                        string.Format(t.StatusHoursLeft, status.HoursLeft) + "\n" +
                        forecastLine + "\n" +
                        string.Join('\n', lines) +
                        (status.Players.Count > 15 ? string.Format(t.StatusMore, status.Players.Count - 15) : ""), ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command handling failed: {Text}", text);
            // Язык здесь резолвим заново: до места, где считается t, выполнение могло
            // и не дойти — например, упало ещё в RefreshUsernameAsync.
            var errText = await TextForAsync(msg, sp, ct);
            var hint = ex switch
            {
                InvalidOperationException ioe when ioe.Message.Contains("CR API") => errText.ErrCrApiToken,
                HttpRequestException => errText.ErrCrApiDown,
                Microsoft.EntityFrameworkCore.DbUpdateException or System.Data.Common.DbException =>
                    string.Format(errText.ErrDb, Describe(ex)),
                _ => string.Format(errText.ErrGeneric, Describe(ex))
            };
            await Reply(msg, hint, ct);
        }
    }

    /// <summary>
    /// «Магия»: пользователь просто отправляет свой CR-тег — бот сразу находит клан
    /// и показывает статус войны. Работает без каких-либо команд.
    /// </summary>
    private async Task HandleQuickLookupAsync(Message msg, string rawTag, IServiceProvider sp, CancellationToken ct)
    {
        var tag = LinkPlayerUseCase.Normalize(rawTag);
        var t = await TextForAsync(msg, sp, ct);

        // Привязываем игрока (без клана — из ЛС)
        var quickReferrer = _pendingReferrals.TryRemove(msg.From!.Id, out var qr) ? qr : (long?)null;
        var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
            .ExecuteAsync(msg.From!.Id, tag, null, quickReferrer, msg.From!.Username, ct);

        if (playerName is null)
        {
            await Reply(msg, string.Format(t.QuickNotFound, tag), ct);
            return;
        }

        var crApi = sp.GetRequiredService<IClashRoyaleApi>();
        string? clanTag = null;
        try { clanTag = await crApi.GetPlayerClanTagAsync(tag, ct); }
        catch { /* not critical */ }

        if (clanTag is null)
        {
            await Reply(msg, string.Format(t.QuickNoClan, playerName), ct);
            return;
        }

        // Auto-register clan in DB if not yet there, then link the player to it.
        // This lets the Mini App show war stats without the leader running /setup.
        var clanRepo = sp.GetRequiredService<IClanRepository>();
        var existingClan = await clanRepo.GetByTagAsync(clanTag, ct);
        if (existingClan is null)
        {
            string? autoName = null;
            try { autoName = await crApi.GetClanNameAsync(clanTag, ct); }
            catch { /* not critical */ }

            if (autoName is not null)
            {
                existingClan = new Clan { ClanTag = clanTag, Name = autoName, TelegramChatId = 0, CreatedAtUtc = DateTime.UtcNow };
                await clanRepo.AddAsync(existingClan, ct);
                await clanRepo.SaveChangesAsync(ct);
            }
        }
        if (existingClan is not null)
        {
            var playerRepo = sp.GetRequiredService<IPlayerRepository>();
            var linkedPlayer = await playerRepo.GetByTelegramIdAsync(msg.From!.Id, ct);
            if (linkedPlayer is not null && linkedPlayer.ClanId != existingClan.Id)
            {
                linkedPlayer.ClanId = existingClan.Id;
                await playerRepo.SaveChangesAsync(ct);
            }
        }

        var getStatus = sp.GetRequiredService<GetClanStatusUseCase>();
        ClanStatusDto? status = null;
        try { status = await getStatus.ExecuteAsync(clanTag, ct); }
        catch { /* not critical */ }

        if (status is null)
        {
            await Reply(msg, string.Format(t.QuickNoWarData, playerName, clanTag), ct);
            return;
        }

        var me = status.Players.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, tag, StringComparison.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(t.QuickHeader, playerName, status.ClanName));
        sb.AppendLine();

        if (status.PeriodType is "warDay" or "colosseum")
        {
            var kind = status.PeriodType == "colosseum" ? t.BriefColosseum : t.BriefWar;
            sb.AppendLine(string.Format(t.QuickWarLine, kind, status.HoursLeft));
            sb.AppendLine(string.Format(t.QuickPlayed, status.Stats.PlayersPlayed, status.Players.Count));

            if (me is not null)
            {
                sb.AppendLine();
                sb.AppendLine(me.DecksUsedToday switch
                {
                    4 => string.Format(t.QuickMeAll, me.Fame, me.Rank),
                    0 => string.Format(t.QuickMeNone, me.Fame, me.Rank),
                    _ => string.Format(t.QuickMeSome, me.DecksUsedToday, me.Fame, me.Rank)
                });

                // Кто ещё не атаковал — короткий список
                var laggards = status.Players
                    .Where(p => p.Status == "notPlayed" && p.PlayerTag != me.PlayerTag)
                    .Take(5)
                    .ToList();
                if (laggards.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(t.QuickLaggardsTitle);
                    foreach (var p in laggards)
                        sb.AppendLine(string.Format(t.QuickLaggardRow, p.Name, p.DecksUsedToday));
                    var totalLaggards = status.Players.Count(p => p.Status == "notPlayed");
                    if (totalLaggards > 5)
                        sb.AppendLine(string.Format(t.QuickAndMore, totalLaggards - 5));
                }
            }
            else
            {
                sb.AppendLine(t.QuickNotInWar);
            }
        }
        else
        {
            sb.AppendLine(t.QuickTraining);
            sb.AppendLine(string.Format(t.QuickMembers, status.Players.Count));
        }

        sb.AppendLine();
        sb.AppendLine(t.QuickFooter);

        // Кнопка "Поделиться с кланом" — открывает нативный Telegram share-диалог.
        // Пользователь сам выбирает чат; никакого спама.
        var shareText = Uri.EscapeDataString(t.QuickShareText);
        var shareUrl = $"https://t.me/share/url?url=https://t.me/{_botUsername}&text={shareText}";
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl(t.QuickShareButton, shareUrl));

        await bot.SendMessage(msg.Chat.Id, sb.ToString(),
            replyParameters: msg.MessageId,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    /// <summary>Похоже на CR-тег: 3–12 буквенно-цифровых символов, можно с # вначале.</summary>
    /// <summary>
    /// Обновляет сохранённый @username, если он изменился. Дёшево: один поиск по
    /// уникальному индексу, запись только при реальном отличии.
    /// </summary>
    private static async Task RefreshUsernameAsync(Message msg, IServiceProvider sp, CancellationToken ct)
    {
        var username = msg.From?.Username;
        if (string.IsNullOrEmpty(username) || msg.From is null) return;

        try
        {
            var players = sp.GetRequiredService<IPlayerRepository>();
            var player = await players.GetByTelegramIdAsync(msg.From.Id, ct);
            if (player is not null && player.TelegramUsername != username)
            {
                player.TelegramUsername = username;
                await players.SaveChangesAsync(ct);
            }
        }
        catch { /* не критично — обработка сообщения важнее */ }
    }

    private static bool IsLikelyCrTag(string text)
    {
        var t = text.Trim();
        if (t.StartsWith('#')) t = t[1..];
        return t.Length is >= 3 and <= 12 && t.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Автор сообщения, на которое реально ответили, или null.
    ///
    /// В форум-супергруппе Telegram заполняет ReplyToMessage у КАЖДОГО сообщения темы:
    /// туда кладётся служебное сообщение о создании темы. Формально это ответ, по смыслу —
    /// нет: человек ни на кого не отвечал. Отличаем корень темы двумя признаками —
    /// служебное поле ForumTopicCreated и совпадение id с идентификатором треда.
    /// </summary>
    private static User? RealReplyAuthor(Message msg)
    {
        var replyTo = msg.ReplyToMessage;
        if (replyTo is null) return null;

        var isTopicRoot = replyTo.ForumTopicCreated is not null
                          || (msg.MessageThreadId is int threadId && replyTo.MessageId == threadId);

        return isTopicRoot ? null : replyTo.From;
    }

    /// <summary>
    /// Похоже ли на юзернейм Telegram: латиница, цифры и подчёркивания, 5–32 символа.
    /// Нужно, чтобы не сохранить в качестве юзернейма имя человека — тег @Максим
    /// в чате просто не сработает, и лидер об этом не узнает.
    /// </summary>
    private static bool IsTelegramUsername(string s) =>
        s.Length is >= 5 and <= 32 && s.All(c => c is '_' || (c < 128 && char.IsLetterOrDigit(c)));

    private static string Describe(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null) root = root.InnerException;
        var msg = root.Message.Length > 180 ? root.Message[..180] + "…" : root.Message;
        return $"{root.GetType().Name}: {msg}";
    }

    private static string Period(string p, BotText t) => p switch
    {
        "warDay" => t.PeriodWarDay,
        "colosseum" => t.PeriodColosseum,
        _ => t.PeriodTraining
    };

    /// <summary>
    /// Админ ли отправитель. GetChatMember — живой сетевой вызов, а проверка стоит
    /// в начале каждой админской команды: серия /bind подряд означала серию запросов
    /// к Bot API. Состав админов меняется редко, поэтому держим ответ 5 минут.
    /// </summary>
    private async Task<bool> IsAdminAsync(Message msg, CancellationToken ct)
    {
        if (msg.Chat.Type == ChatType.Private) return true;

        var key = (msg.Chat.Id, msg.From!.Id);
        if (_adminCache.TryGetValue(key, out var hit) && hit.Until > DateTime.UtcNow)
            return hit.IsAdmin;

        var member = await bot.GetChatMember(msg.Chat.Id, msg.From.Id, ct);
        var isAdmin = member.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator;
        _adminCache[key] = (isAdmin, DateTime.UtcNow.AddMinutes(5));
        return isAdmin;
    }

    /// <summary>
    /// На каком языке отвечать.
    ///
    /// В группе это язык клана, привязанного к чату: сообщение видят все, и выбирать
    /// его должен клан, а не тот, кто последним нажал команду. В личке — язык клана
    /// игрока, а пока он не привязан, язык интерфейса Telegram у самого человека:
    /// другого сигнала о том, на каком языке с ним говорить, в этот момент просто нет.
    ///
    /// Ошибку глотаем намеренно: не смогли определить язык — ответим по-русски,
    /// но ответим. Промолчать в ответ на команду хуже, чем ответить не на том языке.
    /// </summary>
    private static async Task<BotText> TextForAsync(Message msg, IServiceProvider sp, CancellationToken ct)
    {
        try
        {
            var clans = sp.GetRequiredService<IClanRepository>();
            Clan? clan;

            if (msg.Chat.Type == ChatType.Private)
            {
                var players = sp.GetRequiredService<IPlayerRepository>();
                var player = msg.From is null ? null : await players.GetByTelegramIdAsync(msg.From.Id, ct);
                clan = player?.ClanId is int clanId ? await clans.GetByIdAsync(clanId, ct) : null;
            }
            else
            {
                clan = await clans.GetByChatIdAsync(msg.Chat.Id, ct);
            }

            if (clan is not null) return NotificationSettings.Parse(clan.NotificationSettingsJson).Text;
        }
        catch { /* язык — не повод не ответить на команду */ }

        return BotText.For(msg.From?.LanguageCode);
    }

    private Task Reply(Message msg, string text, CancellationToken ct) =>
        bot.SendMessage(msg.Chat.Id, text, replyParameters: msg.MessageId, cancellationToken: ct);
}
