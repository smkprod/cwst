using ClanWarTracker.Application.UseCases;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ClanWarTracker.Worker;

/// <summary>Long polling + команды /setup, /link, /status, /start.</summary>
public class BotUpdateHandler(
    ITelegramBotClient bot,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<BotUpdateHandler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bot.StartReceiving(
            HandleUpdateAsync,
            (_, ex, _) => { logger.LogError(ex, "Bot polling error"); return Task.CompletedTask; },
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
            stoppingToken);

        logger.LogInformation("Bot polling started");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } msg) return;

        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].Split('@')[0]; // "/link@MyBot" -> "/link"
            var arg = parts.Length > 1 ? parts[1] : null;

            switch (command)
            {
                case "/start":
                    await bot.SendMessage(msg.Chat.Id,
                        "⚔️ Clan War Tracker\n\n" +
                        "/setup #ТЕГ_КЛАНА — привязать клан к этой группе (админ)\n" +
                        "/link #ТВОЙ_ТЕГ — привязать свой аккаунт CR\n" +
                        "/status — статус текущей войны\n" +
                        "/remind N — слать напоминания за N часов до конца военного дня (админ, по умолчанию 3)\n\n" +
                        "Открой Mini App кнопкой ниже 👇", cancellationToken: ct);
                    break;

                case "/setup":
                    if (arg is null) { await Reply(msg, "Формат: /setup #ТЕГ_КЛАНА", ct); return; }
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, "Только админ группы может привязать клан.", ct); return; }
                    var clanName = await sp.GetRequiredService<SetupClanUseCase>()
                        .ExecuteAsync(msg.Chat.Id, arg, ct);
                    await Reply(msg, clanName is null
                        ? "❌ Клан не найден. Проверь тег."
                        : $"✅ Клан «{clanName}» привязан к этой группе!", ct);
                    break;

                case "/link":
                    if (arg is null) { await Reply(msg, "Формат: /link #ТВОЙ_ТЕГ", ct); return; }
                    // В ЛС нет группы клана — передаём null, игрок создаётся без клана
                    var isPrivate = msg.Chat.Type == ChatType.Private;
                    var linkChatId = isPrivate ? (long?)null : msg.Chat.Id;
                    var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
                        .ExecuteAsync(msg.From!.Id, arg, linkChatId, ct);
                    await Reply(msg, playerName is null
                        ? "❌ Игрок не найден. Проверь тег (профиль → значок тега)."
                        : isPrivate
                            ? $"✅ Привязан игрок «{playerName}»! Теперь можешь открыть Mini App через кнопку меню. Чтобы получать напоминания о Клан Войне — попроси лидера клана добавить бота в группу клана."
                            : $"✅ Привязан игрок «{playerName}». Теперь буду напоминать тебе про войну в личке — напиши боту /start в ЛС, чтобы он мог писать первым.", ct);
                    break;

                case "/remind":
                    if (!await IsAdminAsync(msg, ct)) { await Reply(msg, "Только админ группы может менять время напоминаний.", ct); return; }
                    if (!int.TryParse(arg, out var hours) || hours is < 1 or > 12)
                    {
                        await Reply(msg, "Формат: /remind N — за сколько часов до конца военного дня напоминать (от 1 до 12).\nНапример: /remind 3", ct);
                        return;
                    }
                    var clanRepo = sp.GetRequiredService<ClanWarTracker.Domain.Interfaces.IClanRepository>();
                    var remindClan = await clanRepo.GetByChatIdAsync(msg.Chat.Id, ct);
                    if (remindClan is null) { await Reply(msg, "Клан не привязан. Сначала /setup #ТЕГ.", ct); return; }
                    remindClan.ReminderHoursBeforeEnd = hours;
                    await clanRepo.SaveChangesAsync(ct);
                    await Reply(msg,
                        $"✅ Автонапоминания будут приходить за {hours} ч до конца военного дня (день войны заканчивается в 10:00 UTC).\n" +
                        $"Напомню только тем, кто к этому времени не отыграл все 4/4 колоды.", ct);
                    break;

                case "/status":
                    var statusUseCase = sp.GetRequiredService<GetClanStatusUseCase>();
                    var clans = sp.GetRequiredService<ClanWarTracker.Domain.Interfaces.IClanRepository>();
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
                // Временно показываем детали неизвестных ошибок прямо в чате — для отладки деплоя
                _ => $"⚠️ Ошибка: {Describe(ex)}"
            };
            await Reply(msg, hint, ct);
        }
    }

    /// <summary>Краткое описание исключения для отладочного ответа в чате.</summary>
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
