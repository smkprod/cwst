using System.Text;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Interfaces;
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
                                  "/remind N — напоминания за N часов до конца дня\n\n" +
                                  "Участники: напишите боту /start в личку и отправьте свой тег CR.",
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
                        .ExecuteAsync(msg.Chat.Id, arg, ct);
                    await Reply(msg, clanName is null
                        ? "❌ Клан не найден. Проверь тег."
                        : $"✅ Клан «{clanName}» привязан к этой группе!\n\n" +
                          "Участники: напишите боту /start в личку и отправьте свой тег CR — сразу увидите статистику.", ct);
                    break;

                case "/link":
                    if (arg is null) { await Reply(msg, "Формат: /link #ТВОЙ_ТЕГ", ct); return; }
                    var isPrivate = msg.Chat.Type == ChatType.Private;
                    var linkChatId = isPrivate ? (long?)null : msg.Chat.Id;
                    var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
                        .ExecuteAsync(msg.From!.Id, arg, linkChatId, ct);
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
        var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
            .ExecuteAsync(msg.From!.Id, tag, null, ct);

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
        sb.AppendLine("Открой Mini App для полной статистики: история, прогнозы, рейтинг 👇");

        await Reply(msg, sb.ToString(), ct);
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
