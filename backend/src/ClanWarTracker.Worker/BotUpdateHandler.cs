using System.Collections.Concurrent;
using System.Text;
using ClanWarTracker.Application.DTOs;
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

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } msg) return;

        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            // Быстрый поиск по тегу: пользователь просто отправляет #ТЕГ без команды
            if (msg.Chat.Type == ChatType.Private && !text.StartsWith('/') && IsLikelyCrTag(text))
            {
                await HandleQuickLookupAsync(msg, text, sp, ct);
                return;
            }

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].Split('@')[0]; // "/link@MyBot" -> "/link"
            var arg = parts.Length > 1 ? parts[1] : null;

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

                        await bot.SendMessage(msg.Chat.Id,
                            "⚔️ Clanify — статистика войны Clash Royale\n\n" +
                            "Отправь свой тег аккаунта CR прямо сюда — например:\n" +
                            "#2VUPLPU0R\n\n" +
                            "Я сразу покажу:\n" +
                            "• кто не атакует в войне твоего клана\n" +
                            "• твой личный счёт и место в рейтинге\n" +
                            "• сколько часов осталось до конца дня\n\n" +
                            "Работает для всех участников — не только лидеров.\n\n" +
                            "Или открой Mini App кнопкой в меню ниже 👇",
                            cancellationToken: ct);
                    }
                    else
                    {
                        var groupClanRepo = sp.GetRequiredService<IClanRepository>();
                        var groupClan = await groupClanRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                        await bot.SendMessage(msg.Chat.Id,
                            groupClan is null
                                ? "⚔️ Clanify — статистика войны Clash Royale\n\n" +
                                  "Чтобы подключить клан к этой группе, лидер или администратор выполняет:\n" +
                                  "/setup #ТЕГ_КЛАНА\n\n" +
                                  "После этого каждый участник может написать боту /start в ЛС и отправить свой тег — и сразу увидит статистику."
                                : $"⚔️ Клан «{groupClan.Name}» подключён!\n" +
                                  "/status — статус текущей войны\n" +
                                  "/remind N — напоминания за N часов до конца дня\n" +
                                  "/nudge — пнуть тех, кто не отыграл (тег по @username)\n" +
                                  "/settopic — слать уведомления в эту тему (запусти внутри темы)\n\n" +
                                  "Участники: напишите боту /start в личку и отправьте свой тег CR.",
                            messageThreadId: msg.MessageThreadId,
                            cancellationToken: ct);
                    }
                    break;

                case "/setup":
                    if (msg.Chat.Type == ChatType.Private)
                    {
                        await Reply(msg, "⚠️ /setup используется только в групповом чате клана, а не в ЛС.", ct);
                        return;
                    }
                    if (arg is null) { await Reply(msg, "Формат: /setup #ТЕГ_КЛАНА", ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, "Только админ группы может привязать клан.", ct); return; }
                    var clanName = await sp.GetRequiredService<SetupClanUseCase>()
                        .ExecuteAsync(msg.Chat.Id, arg, msg.MessageThreadId, ct);
                    var topicNote = msg.MessageThreadId is not null
                        ? "\n\n📌 Напоминания и отчёты будут приходить в эту тему."
                        : "";
                    await Reply(msg, clanName is null
                        ? "❌ Клан не найден. Проверь тег."
                        : $"✅ Клан «{clanName}» привязан к этой группе!\n\n" +
                          "Участники: напишите боту /start в личку и отправьте свой тег CR — сразу увидите статистику." +
                          topicNote, ct);
                    break;

                case "/link":
                    if (arg is null) { await Reply(msg, "Формат: /link #ТВОЙ_ТЕГ", ct); return; }
                    var isPrivate = msg.Chat.Type == ChatType.Private;
                    var linkChatId = isPrivate ? (long?)null : msg.Chat.Id;
                    var linkReferrer = _pendingReferrals.TryRemove(msg.From!.Id, out var lr) ? lr : (long?)null;
                    var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
                        .ExecuteAsync(msg.From!.Id, arg, linkChatId, linkReferrer, msg.From!.Username, ct);
                    await Reply(msg, playerName is null
                        ? "❌ Игрок не найден. Проверь тег (профиль → значок тега)."
                        : isPrivate
                            ? $"✅ Привязан игрок «{playerName}»! Открой Mini App через кнопку меню."
                            : $"✅ Привязан игрок «{playerName}». Напишите боту /start в личку — буду присылать напоминания.", ct);
                    break;

                case "/remind":
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, "Только админ группы может менять время напоминаний.", ct); return; }
                    if (!int.TryParse(arg, out var hours) || hours is < 1 or > 12)
                    {
                        await Reply(msg, "Формат: /remind N — за сколько часов до конца военного дня напоминать (от 1 до 12).\nНапример: /remind 3", ct);
                        return;
                    }
                    var clanRepo = sp.GetRequiredService<IClanRepository>();
                    var remindClan = await clanRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (remindClan is null) { await Reply(msg, "Клан не привязан. Сначала /setup #ТЕГ.", ct); return; }
                    remindClan.ReminderHoursBeforeEnd = hours;
                    await clanRepo.SaveChangesAsync(ct);
                    await Reply(msg,
                        $"✅ Автонапоминания будут приходить за {hours} ч до конца военного дня (день войны заканчивается в 10:00 UTC).\n" +
                        $"Напомню только тем, кто к этому времени не отыграл все 4/4 колоды.", ct);
                    break;

                case "/settopic":
                case "/topic":
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, "⚠️ Команда работает только в групповом чате клана.", ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, "Менять тему для уведомлений может только админ группы.", ct); return; }
                    var topicRepo = sp.GetRequiredService<IClanRepository>();
                    var topicClan = await topicRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (topicClan is null) { await Reply(msg, "Клан не привязан. Сначала /setup #ТЕГ.", ct); return; }
                    topicClan.TelegramMessageThreadId = msg.MessageThreadId;
                    await topicRepo.SaveChangesAsync(ct);
                    await Reply(msg, msg.MessageThreadId is not null
                        ? "📌 Готово! Теперь напоминания, теги и отчёты бот будет слать в эту тему."
                        : "📌 Готово! Уведомления будут приходить в общий чат (не в тему). Запусти /settopic внутри нужной темы, чтобы привязать её.", ct);
                    break;

                case "/nudge":
                case "/пни":
                    if (msg.Chat.Type == ChatType.Private) { await Reply(msg, "⚠️ Команда работает в групповом чате клана.", ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, "Пинать игроков может только админ группы.", ct); return; }
                    var nudgeRepo = sp.GetRequiredService<IClanRepository>();
                    var nudgeClan = await nudgeRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (nudgeClan is null) { await Reply(msg, "Клан не привязан. Сначала /setup #ТЕГ.", ct); return; }
                    var isProNudge = nudgeClan.EffectivePlan(DateTime.UtcNow) == PlanTier.Pro;
                    var nudgeResult = await sp.GetRequiredService<NudgePlayersUseCase>()
                        .ExecuteAsync(nudgeClan.Id, isProNudge, ct);
                    if (nudgeResult is null) { await Reply(msg, "Сейчас не день войны — пинать некого.", ct); return; }
                    if (nudgeResult.TaggableCount == 0 && nudgeResult.UnlinkedCount == 0)
                        await Reply(msg, "Все уже отыграли 4/4 — пинать некого 🎉", ct);
                    else if (nudgeResult.TaggableCount == 0)
                        await Reply(msg, $"{nudgeResult.UnlinkedCount} не доиграли, но никто из них не привязал Telegram — тегнуть некого. Пусть откроют бота и привяжут аккаунт.", ct);
                    break;

                case "/status":
                    var statusUseCase = sp.GetRequiredService<GetClanStatusUseCase>();
                    var clans = sp.GetRequiredService<IClanRepository>();
                    var clan = await clans.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (clan is null) { await Reply(msg, "Клан не привязан. Сначала /setup #ТЕГ.", ct); return; }

                    var status = await statusUseCase.ExecuteAsync(clan.ClanTag, ct);
                    if (status is null) { await Reply(msg, "Не удалось получить данные войны.", ct); return; }

                    var played = status.Players.Count(p => p.Status == "played");
                    var lines = status.Players.Take(15).Select(p => p.Status switch
                    {
                        "played" => $"✅ {p.Name} ({p.DecksUsedToday}/4)",
                        "notPlayed" => $"❌ {p.Name} ({p.DecksUsedToday}/4)",
                        _ => $"⏳ {p.Name} ({p.DecksUsedToday}/4)"
                    });
                    var forecastLine = status.Forecast is null || status.PeriodType == "training"
                        ? ""
                        : $"🔮 Прогноз: {status.Forecast.ProjectedDayFame:N0} к концу дня, {status.Forecast.ProjectedWeekFame:N0} за неделю\n";
                    await Reply(msg,
                        $"⚔️ {status.ClanName} — {Period(status.PeriodType)}\n" +
                        $"Сыграли полностью: {played}/{status.Players.Count}\n" +
                        $"До конца дня: ~{status.HoursLeft} ч\n" +
                        forecastLine + "\n" +
                        string.Join('\n', lines) +
                        (status.Players.Count > 15 ? $"\n… и ещё {status.Players.Count - 15}. Полный список — в Mini App." : ""), ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command handling failed: {Text}", text);
            var hint = ex switch
            {
                InvalidOperationException ioe when ioe.Message.Contains("CR API") =>
                    "⚠️ Clash Royale API отклонил запрос — ключ привязан к другому IP. Админ, проверь CLASH_ROYALE_API_TOKEN.",
                HttpRequestException =>
                    "⚠️ Clash Royale API недоступен. Попробуй через пару минут.",
                Microsoft.EntityFrameworkCore.DbUpdateException or System.Data.Common.DbException =>
                    $"⚠️ Ошибка базы данных: {Describe(ex)}",
                _ => $"⚠️ Ошибка: {Describe(ex)}"
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

        // Привязываем игрока (без клана — из ЛС)
        var quickReferrer = _pendingReferrals.TryRemove(msg.From!.Id, out var qr) ? qr : (long?)null;
        var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
            .ExecuteAsync(msg.From!.Id, tag, null, quickReferrer, msg.From!.Username, ct);

        if (playerName is null)
        {
            await Reply(msg,
                $"❌ Игрок {tag} не найден в Clash Royale.\n\n" +
                "Проверь тег — он виден в профиле под именем (выглядит как #ABC123).\n" +
                "Или отправь /start чтобы узнать подробнее.", ct);
            return;
        }

        var crApi = sp.GetRequiredService<IClashRoyaleApi>();
        string? clanTag = null;
        try { clanTag = await crApi.GetPlayerClanTagAsync(tag, ct); }
        catch { /* not critical */ }

        if (clanTag is null)
        {
            await Reply(msg,
                $"✅ Привязан: {playerName}\n\n" +
                "Ты сейчас не в клане — война недоступна.\n" +
                "Открой Mini App через кнопку меню бота 🎮", ct);
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
                existingClan = new Clan { ClanTag = clanTag, Name = autoName, TelegramChatId = 0 };
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
            await Reply(msg,
                $"✅ Привязан: {playerName}\n" +
                $"Клан: {clanTag}\n\n" +
                "Данные войны сейчас недоступны. Открой Mini App через кнопку меню 🎮", ct);
            return;
        }

        var me = status.Players.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, tag, StringComparison.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        sb.AppendLine($"✅ {playerName}  •  {status.ClanName}");
        sb.AppendLine();

        if (status.PeriodType is "warDay" or "colosseum")
        {
            var kind = status.PeriodType == "colosseum" ? "Колизей" : "Война";
            sb.AppendLine($"⚔️ {kind} — до конца дня: ~{status.HoursLeft} ч");
            sb.AppendLine($"Отыграли сегодня: {status.Stats.PlayersPlayed}/{status.Players.Count}");

            if (me is not null)
            {
                sb.AppendLine();
                sb.AppendLine(me.DecksUsedToday switch
                {
                    4 => $"Ты: ✅ все 4 колоды — молодец! Слава: {me.Fame} 🏆 (#{me.Rank})",
                    0 => $"Ты: ❌ ещё не атаковал сегодня! Слава: {me.Fame} 🏆 (#{me.Rank})",
                    _ => $"Ты: ⏳ {me.DecksUsedToday}/4 колоды. Слава: {me.Fame} 🏆 (#{me.Rank})"
                });

                // Кто ещё не атаковал — короткий список
                var laggards = status.Players
                    .Where(p => p.Status == "notPlayed" && p.PlayerTag != me.PlayerTag)
                    .Take(5)
                    .ToList();
                if (laggards.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Не отыграли сегодня:");
                    foreach (var p in laggards)
                        sb.AppendLine($"  ❌ {p.Name} ({p.DecksUsedToday}/4)");
                    var totalLaggards = status.Players.Count(p => p.Status == "notPlayed");
                    if (totalLaggards > 5)
                        sb.AppendLine($"  … и ещё {totalLaggards - 5}");
                }
            }
            else
            {
                sb.AppendLine("\nТебя нет в составе этой войны.");
            }
        }
        else
        {
            sb.AppendLine("📋 Сейчас тренировочная неделя.");
            sb.AppendLine($"Участников в клане: {status.Players.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("Полная статистика — в Mini App: история, прогнозы, рейтинг 👇");

        // Кнопка "Поделиться с кланом" — открывает нативный Telegram share-диалог.
        // Пользователь сам выбирает чат; никакого спама.
        var shareText = Uri.EscapeDataString(
            "⚔️ Слежу за Clan War через этот бот — отправь свой тег CR и сразу увидишь статистику войны своего клана");
        var shareUrl = $"https://t.me/share/url?url=https://t.me/{_botUsername}&text={shareText}";
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("📤 Поделиться с кланом", shareUrl));

        await bot.SendMessage(msg.Chat.Id, sb.ToString(),
            replyParameters: msg.MessageId,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    /// <summary>Похоже на CR-тег: 3–12 буквенно-цифровых символов, можно с # вначале.</summary>
    private static bool IsLikelyCrTag(string text)
    {
        var t = text.Trim();
        if (t.StartsWith('#')) t = t[1..];
        return t.Length is >= 3 and <= 12 && t.All(char.IsLetterOrDigit);
    }

    private static string Describe(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null) root = root.InnerException;
        var msg = root.Message.Length > 180 ? root.Message[..180] + "…" : root.Message;
        return $"{root.GetType().Name}: {msg}";
    }

    private static string Period(string p) => p switch
    {
        "warDay" => "День войны",
        "colosseum" => "Колизей",
        _ => "Тренировка"
    };

    private async Task<bool> IsAdminAsync(Message msg, CancellationToken ct)
    {
        if (msg.Chat.Type == ChatType.Private) return true;
        var member = await bot.GetChatMember(msg.Chat.Id, msg.From!.Id, ct);
        return member.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator;
    }

    private Task Reply(Message msg, string text, CancellationToken ct) =>
        bot.SendMessage(msg.Chat.Id, text, replyParameters: msg.MessageId, cancellationToken: ct);
}
