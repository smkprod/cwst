using System.Collections.Concurrent;
using System.Text;
using ClanWarTracker.Application.DTOs;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Application.Security;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace ClanWarTracker.Worker;

/// <summary>Long polling + команды /setup, /link, /status, /start.</summary>
public class BotUpdateHandler(
    ITelegramBotClient bot,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ServiceAccessOptions ownerAccess,
    ILogger<BotUpdateHandler> logger) : BackgroundService
{
    private string _botUsername = "bot";

    /// <summary>
    /// Публичный адрес сервиса — по нему Telegram скачивает картинки карточек.
    /// Не задан — inline работает текстом: лучше карточка без картинки, чем ссылка
    /// в никуда, по которой Telegram молча выбросит результат из выдачи.
    /// </summary>
    private string? PublicBaseUrl => config["PUBLIC_BASE_URL"]?.Trim().TrimEnd('/') is { Length: > 0 } url
        ? url
        : null;

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
            // MyChatMember обязателен: без него бот не узнаёт, что его добавили в группу,
            // и молчит. Человек добавил бота, ничего не произошло — и он уходит, так и
            // не поняв, что дальше. Приветствие срабатывало только на ручной /start,
            // которого никто не пишет.
            //
            // PreCheckoutQuery — для оплаты звёздами. Без него Telegram не доносит до
            // бота запрос «можно списывать?», бот на него не отвечает, и каждая оплата
            // падает через десять секунд ожидания — у плательщика, а не у нас в логах.
            new ReceiverOptions
            {
                // CallbackQuery — кнопки под сообщениями бота («Это мой аккаунт»).
                AllowedUpdates =
                [
                    UpdateType.Message, UpdateType.InlineQuery, UpdateType.MyChatMember,
                    UpdateType.PreCheckoutQuery, UpdateType.CallbackQuery,
                ],
            },
            stoppingToken);

        await RegisterPrivateCommandsAsync(stoppingToken);
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
        // Inline-запрос идёт мимо общей очереди: Telegram ждёт ответа считанные секунды,
        // и вставать за медленной командой из чужого чата ему нельзя.
        if (update.InlineQuery is { } inline)
        {
            _ = Task.Run(async () =>
            {
                try { await ProcessInlineQueryAsync(inline, ct); }
                catch (OperationCanceledException) { /* воркер останавливается */ }
                catch (Exception ex) { logger.LogError(ex, "Inline query failed"); }
            });
            return Task.CompletedTask;
        }

        if (update.MyChatMember is { } membership)
        {
            _ = Task.Run(async () =>
            {
                try { await ProcessMembershipAsync(membership, ct); }
                catch (OperationCanceledException) { /* воркер останавливается */ }
                catch (Exception ex) { logger.LogError(ex, "Membership update failed"); }
            });
            return Task.CompletedTask;
        }

        if (update.CallbackQuery is { } callback)
        {
            _ = Task.Run(async () =>
            {
                try { await ProcessCallbackAsync(callback, ct); }
                catch (OperationCanceledException) { /* воркер останавливается */ }
                catch (Exception ex) { logger.LogError(ex, "Callback {Data} failed", callback.Data); }
            });
            return Task.CompletedTask;
        }

        // Подтверждение оплаты — мимо общей очереди: на ответ у бота десять секунд,
        // и ждать за медленной командой из чужого чата ему нельзя.
        if (update.PreCheckoutQuery is { } checkout)
        {
            _ = Task.Run(async () =>
            {
                try { await ProcessPreCheckoutAsync(checkout, ct); }
                catch (OperationCanceledException) { /* воркер останавливается */ }
                catch (Exception ex) { logger.LogError(ex, "Pre-checkout {Id} failed", checkout.Id); }
            });
            return Task.CompletedTask;
        }

        // Сообщение об оплате — ДО фильтра по тексту ниже: текста у него нет, и
        // фильтр отбросил бы его молча. Звёзды списаны, а спонсорство не выдано.
        if (update.Message is { SuccessfulPayment: { } paid } payMsg)
        {
            _ = Task.Run(async () =>
            {
                try { await ProcessSuccessfulPaymentAsync(payMsg, paid, ct); }
                catch (OperationCanceledException) { /* воркер останавливается */ }
                catch (Exception ex)
                {
                    // Громко и с номером платежа: по нему делается возврат, и без него
                    // деньги человеку вернуть не по чему.
                    logger.LogError(ex, "Successful payment NOT applied: charge {Charge}, payload {Payload}",
                        paid.TelegramPaymentChargeId, paid.InvoicePayload);
                }
            });
            return Task.CompletedTask;
        }

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

    /// <summary>Сколько ждём данные войны, прежде чем ответить тем, что есть.</summary>
    private static readonly TimeSpan InlineBudget = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Inline-режим: `@бот` в ЛЮБОМ чате Telegram отдаёт карточку со своей войной
    /// или с кланом. Смысл не только в удобстве — карточка уезжает туда, где про клан
    /// никто не знает, и каждая такая отправка показывает бота новым людям. До сих пор
    /// он рос только через реф-ссылки внутри клана.
    ///
    /// Telegram ждёт ответа несколько секунд, поэтому на сбор данных стоит бюджет:
    /// не успели — отдаём карточку-приглашение вместо молчания. Пустой ответ выглядит
    /// как сломанный бот, а это худшая реклама из возможных.
    /// </summary>
    /// <summary>
    /// «Можно списывать?» — последний момент, когда оплату ещё можно отклонить.
    /// Отвечаем всегда, даже на ошибке проверки: молчание Telegram тоже засчитает
    /// как отказ, но через десять секунд и без объяснения.
    /// </summary>
    private async Task ProcessPreCheckoutAsync(
        Telegram.Bot.Types.Payments.PreCheckoutQuery q, CancellationToken ct)
    {
        string? problem;
        try
        {
            using var scope = scopeFactory.CreateScope();
            // Оба товара приходят сюда же; различаются префиксом счёта.
            problem = PlusSales.IsPlusPayload(q.InvoicePayload)
                ? await scope.ServiceProvider.GetRequiredService<ProcessPlusPaymentUseCase>()
                    .ValidateAsync(q.Currency, q.TotalAmount, q.InvoicePayload)
                : await scope.ServiceProvider.GetRequiredService<ProcessSponsorPaymentUseCase>()
                    .ValidateAsync(q.Currency, q.TotalAmount, q.InvoicePayload, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pre-checkout validation crashed for {Payload}", q.InvoicePayload);
            problem = "Не получилось проверить оплату — попробуй через минуту.";
        }

        // Сначала ответ, потом след: на ответ у бота десять секунд, на след — сколько угодно
        // errorMessage null — «списывайте», иначе — отказ с этим текстом плательщику
        await bot.AnswerPreCheckoutQuery(q.Id, problem, cancellationToken: ct);
        if (problem is not null)
            logger.LogWarning("Pre-checkout rejected: {Problem} ({Payload})", problem, q.InvoicePayload);

        await TraceAsync(PaymentTrace.CheckoutKey, q.InvoicePayload, null, problem ?? "ok — разрешено списать", ct);
    }

    /// <summary>
    /// Звёзды списаны — выдаём спонсорство и говорим об этом человеку.
    ///
    /// Каждый исход пишется в след для панели и каждый сбой сообщается плательщику
    /// с номером платежа. Первая версия молчала: любую ошибку сохранения она
    /// принимала за повторную доставку и писала в лог как штатное событие, так что
    /// у человека звёзды списывались, а дальше не происходило ничего — ни звезды,
    /// ни сообщения, ни записи в журнале.
    /// </summary>
    private async Task ProcessSuccessfulPaymentAsync(
        Message msg, Telegram.Bot.Types.Payments.SuccessfulPayment paid, CancellationToken ct)
    {
        var charge = paid.TelegramPaymentChargeId;
        var payload = paid.InvoicePayload;

        // Раньше всего остального: если дальше упадёт, по следу будет видно, что
        // подтверждение до бота дошло, и искать надо в выдаче, а не в доставке.
        await TraceAsync(PaymentTrace.PaidKey, payload, charge, "получено, выдаю…", ct);

        if (PlusSales.IsPlusPayload(payload))
        {
            await ProcessPlusPaidAsync(msg, paid, ct);
            return;
        }

        ProcessSponsorPaymentUseCase.Applied? applied = null;
        Exception? failure = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var payments = scope.ServiceProvider.GetRequiredService<ProcessSponsorPaymentUseCase>();
            applied = await payments.ApplyAsync(msg.From?.Id ?? msg.Chat.Id, payload, paid.TotalAmount, charge, ct);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // Повтором считаем, только если платёж с этим номером действительно уже в
        // журнале. Проверка в новом контексте: прежний после упавшего сохранения
        // держит несохранённые записи и повторил бы ту же ошибку.
        if (failure is DbUpdateException && await PaymentRecordedAsync(charge, ct))
        {
            logger.LogInformation("Duplicate payment {Charge} ignored", charge);
            await TraceAsync(PaymentTrace.PaidKey, payload, charge, "повторная доставка — уже выдано", ct);
            return;
        }

        if (failure is not null || applied is null)
        {
            var reason = failure is null
                ? "счёт не разобран или игрок не найден"
                : $"{failure.GetType().Name}: {Flatten(failure)}";
            logger.LogError(failure, "Successful payment NOT applied: charge {Charge}, payload {Payload}, reason {Reason}",
                charge, payload, reason);
            await TraceAsync(PaymentTrace.PaidKey, payload, charge, "НЕ ВЫДАНО — " + reason, ct);
            await TellAsync(msg.Chat.Id,
                "Оплата прошла, но выдать спонсорство автоматически не получилось. " +
                "Напиши владельцу бота и перешли это сообщение — по номеру платежа он выдаст " +
                "спонсорство вручную:\n" + charge, ct);
            return;
        }

        if (applied.Duplicate)
        {
            await TraceAsync(PaymentTrace.PaidKey, payload, charge, "повторная доставка — уже выдано", ct);
            return;
        }

        logger.LogInformation("Sponsor paid: {Name} +{Days}d for {Stars} XTR, charge {Charge}",
            applied.PlayerName, applied.Days, paid.TotalAmount, charge);
        await TraceAsync(PaymentTrace.PaidKey, payload, charge,
            $"ok — {applied.PlayerName} спонсор до {applied.Until:dd.MM.yyyy}", ct);

        await TellAsync(msg.Chat.Id,
            $"★ Спасибо! Спонсорство для {applied.PlayerName} активно до {applied.Until:dd.MM.yyyy}.\n\n" +
            "Фон себе и клану выбираются во вкладке «Я» → «Моё оформление».", ct);
    }

    /// <summary>
    /// Звёзды за «Плюс» списаны — выдаём срок и говорим об этом человеку. Тот же
    /// порядок, что у спонсорства: след для панели на каждом исходе и номер платежа
    /// плательщику при любом сбое, чтобы было по чему вернуть звёзды.
    /// </summary>
    private async Task ProcessPlusPaidAsync(
        Message msg, Telegram.Bot.Types.Payments.SuccessfulPayment paid, CancellationToken ct)
    {
        var charge = paid.TelegramPaymentChargeId;
        var payload = paid.InvoicePayload;

        ProcessPlusPaymentUseCase.Applied? applied = null;
        Exception? failure = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            applied = await scope.ServiceProvider.GetRequiredService<ProcessPlusPaymentUseCase>()
                .ApplyAsync(msg.From?.Id ?? msg.Chat.Id, payload, paid.TotalAmount, charge, ct);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        if (failure is DbUpdateException && await PaymentRecordedAsync(charge, ct))
        {
            await TraceAsync(PaymentTrace.PaidKey, payload, charge, "повторная доставка — уже выдано", ct);
            return;
        }

        if (failure is not null || applied is null)
        {
            var reason = failure is null ? "счёт не разобран" : $"{failure.GetType().Name}: {Flatten(failure)}";
            logger.LogError(failure, "Plus payment NOT applied: charge {Charge}, payload {Payload}, reason {Reason}",
                charge, payload, reason);
            await TraceAsync(PaymentTrace.PaidKey, payload, charge, "НЕ ВЫДАНО — " + reason, ct);
            await TellAsync(msg.Chat.Id,
                "Оплата прошла, но включить Плюс автоматически не получилось. Напиши /paysupport " +
                "и перешли это сообщение — по номеру платежа всё включат вручную или вернут звёзды:\n" + charge, ct);
            return;
        }

        if (applied.Duplicate)
        {
            await TraceAsync(PaymentTrace.PaidKey, payload, charge, "повторная доставка — уже выдано", ct);
            return;
        }

        logger.LogInformation("Plus paid: tg {Tg} +{Days}d for {Stars} XTR, charge {Charge}",
            applied.RecipientTelegramUserId, applied.Days, paid.TotalAmount, charge);
        await TraceAsync(PaymentTrace.PaidKey, payload, charge,
            $"ok — Плюс до {applied.Until:dd.MM.yyyy} (tg {applied.RecipientTelegramUserId})", ct);

        if (applied.Gift)
        {
            // Подарок или оплаченная просьба «подари мне»: спасибо тому, кто платил,
            // и новость тому, кому досталось, - на его языке.
            var payerText = BotText.For(msg.From?.LanguageCode);
            await TellAsync(msg.Chat.Id, string.Format(payerText.GiftSent,
                applied.RecipientName ?? "—", applied.Until.ToString("dd.MM.yyyy")), ct);

            var giver = msg.From?.Username is { Length: > 0 } u ? "@" + u : msg.From?.FirstName ?? "—";
            var recipientText = BotText.For(applied.RecipientLang);
            await TellAsync(applied.RecipientTelegramUserId, string.Format(recipientText.GiftReceived,
                giver, applied.Until.ToString("dd.MM.yyyy")), ct);
            return;
        }

        await TellAsync(msg.Chat.Id,
            $"💎 Спасибо! Clanify Плюс активен до {applied.Until:dd.MM.yyyy}.\n\n" +
            "🧊 «Стоп-тильт» уже включён: напишу «стоп» прямо во время серии поражений — с паузой и итогом захода.\n" +
            "🔬 Полный разбор боёв — во вкладке «Я».\n\n" +
            "Правила (после 2 или 3 поражений, лимит на вечер, тихие часы) — в приложении: «Я» → «🧊 Стоп-тильт».", ct);
    }

    /// <summary>След оплаты для панели — в своём контексте, чтобы упавшая выдача его не утянула.</summary>
    private async Task TraceAsync(string key, string payload, string? charge, string outcome, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<IServiceSettingRepository>();
            await PaymentTrace.RecordAsync(settings, key, payload, charge, outcome, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Payment trace not written"); }
    }

    private async Task<bool> PaymentRecordedAsync(string charge, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISponsorPaymentRepository>();
            return await repo.ExistsAsync(charge, ct);
        }
        catch { return false; }
    }

    /// <summary>Сообщение плательщику. Он мог заблокировать бота — это не повод ронять обработку.</summary>
    private async Task TellAsync(long chatId, string text, CancellationToken ct)
    {
        try { await bot.SendMessage(chatId, text, cancellationToken: ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not message payer {Chat}", chatId); }
    }

    /// <summary>
    /// Текст ошибки вместе с вложенными. У ошибки сохранения своё сообщение пустое
    /// («see the inner exception»), а настоящая причина — во вложенной ошибке базы.
    /// </summary>
    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException) parts.Add(e.Message);
        return string.Join(" → ", parts);
    }

    private async Task ProcessInlineQueryAsync(InlineQuery inline, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var t = BotText.For(inline.From.LanguageCode);
        var results = new List<InlineQueryResult>();
        InlineQueryResultsButton? button = null;

        try
        {
            var players = sp.GetRequiredService<IPlayerRepository>();
            var player = await players.GetByTelegramIdAsync(inline.From.Id, ct);

            // Набрали тег — значит спрашивают про конкретного игрока, а не про себя.
            // Свои карточки в этом случае только мешали бы: человек ищет чужой профиль.
            var query = inline.Query?.Trim() ?? "";
            if (IsLikelyCrTag(query))
            {
                using var searchBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                searchBudget.CancelAfter(InlineBudget);

                var tag = LinkPlayerUseCase.Normalize(query);
                var crSearch = sp.GetRequiredService<IClashRoyaleApi>();
                var found = await Safe(() => crSearch.GetPlayerInfoAsync(tag, searchBudget.Token));
                var searchCatalog = await Safe(() => crSearch.GetAllCardsAsync(searchBudget.Token));

                if (found is not null)
                {
                    results.AddRange(BuildInlineCards(null, found, searchCatalog, tag, t, forSearch: true));
                }
                else
                {
                    results.Add(Card("notfound", t.InlineNotFoundTitle, t.InlineNotFoundDesc,
                        string.Format(t.InlineNotFoundText, tag), t, null));
                }

                await bot.AnswerInlineQuery(inline.Id, results, cacheTime: 300, isPersonal: false,
                    cancellationToken: ct);
                return;
            }

            if (player is not null)
            {
                var clan = player.ClanId is int clanId
                    ? await sp.GetRequiredService<IClanRepository>().GetByIdAsync(clanId, ct)
                    : null;
                if (clan is not null) t = NotificationSettings.Parse(clan.NotificationSettingsJson).Text;

                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                budget.CancelAfter(InlineBudget);

                // Параллельно и под общим бюджетом: война и профиль — независимые запросы,
                // а последовательно они вдвоём в отведённые секунды уже не помещались.
                var crApi = sp.GetRequiredService<IClashRoyaleApi>();
                var statusTask = clan is null
                    ? Task.FromResult<ClanStatusDto?>(null)
                    : Safe(() => sp.GetRequiredService<GetClanStatusUseCase>()
                        .ExecuteAsync(clan.ClanTag, budget.Token));
                var clanTask = clan is null
                    ? Task.FromResult<CrClanInfo?>(null)
                    : Safe(() => crApi.GetClanInfoAsync(clan.ClanTag, budget.Token));
                var infoTask = Safe(() => crApi.GetPlayerInfoAsync(player.PlayerTag, budget.Token));
                var catalogTask = Safe(() => crApi.GetAllCardsAsync(budget.Token));
                var topTask = Safe(() => crApi.GetTopPlayerDecksAsync(20, budget.Token));

                await Task.WhenAll(statusTask, clanTask, infoTask, catalogTask, topTask);

                // Что успело прийти, из того и собираем: одна отвалившаяся ручка
                // не должна уносить с собой остальные карточки.
                results.AddRange(BuildInlineCards(
                    statusTask.Result, infoTask.Result, catalogTask.Result, player.PlayerTag, t,
                    clanInfo: clanTask.Result, topDecks: topTask.Result));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Inline data lookup failed");
        }

        // Нечего показать (не привязан, нет войны, не успели) — зовём к себе,
        // и кнопкой «привязать», которая открывает бота в личке.
        if (results.Count == 0)
        {
            results.Add(new InlineQueryResultArticle(
                "nolink", t.InlineNoLinkTitle, PlainText(t.InlineNoLinkText + t.InlineFooter))
            {
                Description = t.InlineNoLinkDesc,
                ReplyMarkup = OpenBotKeyboard(t),
            });
            button = new InlineQueryResultsButton(t.InlineLinkButton) { StartParameter = "inline" };
        }

        // isPersonal обязателен: карточка своя у каждого, и Telegram не должен
        // показать чужую статистику следующему, кто наберёт то же самое.
        await bot.AnswerInlineQuery(inline.Id, results, cacheTime: 30, isPersonal: true,
            button: button, cancellationToken: ct);
    }

    /// <summary>Выполняет запрос, проглатывая любой сбой: подвела одна ручка — покажем остальное.</summary>
    private static async Task<T?> Safe<T>(Func<Task<T?>> get) where T : class
    {
        try { return await get(); }
        catch { return null; }
    }

    private IEnumerable<InlineQueryResult> BuildInlineCards(
        ClanStatusDto? status, CrPlayerInfo? info,
        IReadOnlyDictionary<string, CrCatalogCard>? catalog, string playerTag, BotText t,
        CrClanInfo? clanInfo = null, List<CrTopDeck>? topDecks = null, bool forSearch = false)
    {
        var me = status?.Players.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, playerTag, StringComparison.OrdinalIgnoreCase));

        // Иконка любимой карты вместо безликой заглушки в списке результатов
        string? favIcon = null;
        if (info?.CurrentFavouriteCard is { } fav && catalog is not null
            && catalog.TryGetValue(fav, out var favCard)) favIcon = favCard.IconUrl;

        if (me is not null && status is not null)
        {
            // Картинкой — только если известен публичный адрес: ссылка в никуда
            // заставит Telegram молча выбросить результат из выдачи, и человек
            // решит, что бот сломался. Нет адреса — та же карточка текстом.
            yield return Photo("war", Img("war", me.PlayerTag),
                t.InlineWarTitle, t.InlineWarDesc,
                string.Format(t.InlineWarText,
                    me.Name, status.ClanName, me.Fame, me.Rank, me.DecksUsedToday),
                t, favIcon);
        }

        if (info is not null)
        {
            // При поиске по тегу это чужой профиль — и подписать его надо иначе,
            // иначе человек решит, что бот показывает ему его собственный.
            var profileText = string.Format(t.InlineProfileText,
                info.Name, info.ExpLevel, info.Trophies, info.BestTrophies,
                info.WarDayWins, info.ThreeCrownWins);
            yield return Photo("profile", Img("profile", info.Tag),
                forSearch ? t.InlineFoundTitle : t.InlineProfileTitle,
                forSearch ? t.InlineFoundDesc : t.InlineProfileDesc,
                profileText, t, favIcon);

            if (info.CurrentDeck.Count > 0)
            {
                var names = string.Join(" · ", info.CurrentDeck.Select(c => $"{c.Name} {c.Level}"));
                var avg = Math.Round(info.CurrentDeck.Average(c => (double)c.Level), 1);
                var link = DeckLink(info.CurrentDeck, catalog);

                var deckText = string.Format(t.InlineDeckText, info.Name, names, avg);
                // Кнопка «открыть в игре» только когда ссылка собралась целиком:
                // неполная открыла бы не ту колоду. Нет ссылки — обычная кнопка бота.
                var deckKeys = link is null
                    ? OpenBotKeyboard(t)
                    : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(t.InlineDeckOpen, link));
                var deckImg = Img("deck", info.Tag);

                yield return deckImg is null
                    ? new InlineQueryResultArticle("deck", t.InlineDeckTitle,
                          PlainText(deckText + t.InlineFooter))
                      {
                          Description = t.InlineDeckDesc,
                          ThumbnailUrl = info.CurrentDeck[0].IconUrl,
                          ReplyMarkup = deckKeys,
                      }
                    : new InlineQueryResultPhoto("deck", deckImg, deckImg)
                      {
                          Title = t.InlineDeckTitle,
                          Description = t.InlineDeckDesc,
                          Caption = deckText + t.InlineFooter,
                          PhotoWidth = CardWidth,
                          PhotoHeight = DeckCardHeight,
                          ReplyMarkup = deckKeys,
                      };
            }
        }

        if (clanInfo is not null)
        {
            yield return Photo("claninfo", Img("clan", clanInfo.Tag),
                t.InlineClanCardTitle, t.InlineClanCardDesc,
                string.Format(t.InlineClanCardText,
                    clanInfo.Name, clanInfo.Tag, clanInfo.MemberCount, clanInfo.ClanScore,
                    clanInfo.ClanWarTrophies, clanInfo.RequiredTrophies),
                t, favIcon);
        }

        if (topDecks is { Count: > 0 })
        {
            // Показываем три колоды поимённо: «так играет игрок №1 мира» весомее любой
            // усреднённой меты, а восемь карт в строку читаются и без картинок.
            var shown = topDecks.Take(3).ToList();
            var rows = string.Join("\n\n", shown.Select(d =>
                $"#{d.Rank} {d.PlayerName} · {d.Trophies} 🏆\n" +
                string.Join(" · ", d.Cards.Select(c => c.Name))));

            var firstLink = DeckLink(shown[0].Cards, catalog);
            yield return new InlineQueryResultArticle(
                "topdecks", t.InlineTopDecksTitle,
                PlainText(string.Format(t.InlineTopDecksText, topDecks.Count, rows) + t.InlineFooter))
            {
                Description = t.InlineTopDecksDesc,
                ThumbnailUrl = shown[0].Cards.FirstOrDefault()?.IconUrl,
                ReplyMarkup = firstLink is null
                    ? OpenBotKeyboard(t)
                    : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(t.InlineTopDeckOne, firstLink)),
            };
        }

        if (status is null) yield break;

        var ours = status.Race.FirstOrDefault(r => r.IsOurClan);
        if (ours is not null)
        {
            yield return Card("clan", t.InlineClanTitle, t.InlineClanDesc,
                string.Format(t.InlineClanText,
                    status.ClanName, ours.Position, status.Race.Count,
                    ours.Fame, status.Stats.PlayersNotPlayed),
                t, favIcon);
        }

        var top = status.Players.Where(p => p.Fame > 0)
            .OrderByDescending(p => p.Fame).Take(3).ToList();
        if (top.Count > 0)
        {
            var medals = new[] { "🥇", "🥈", "🥉" };
            var rows = string.Join("\n", top.Select((p, i) => $"{medals[i]} {p.Name} — {p.Fame} 🏅"));
            yield return Card("top", t.InlineTopTitle, t.InlineTopDesc,
                string.Format(t.InlineTopText, status.ClanName, rows), t, favIcon);
        }

        var last = status.WarLog.FirstOrDefault()?.Standings.FirstOrDefault(s => s.IsOurClan);
        if (last is not null)
        {
            yield return Card("lastwar", t.InlineLastWarTitle, t.InlineLastWarDesc,
                string.Format(t.InlineLastWarText,
                    status.ClanName, last.Rank, last.Fame,
                    last.TrophyChange > 0 ? $"+{last.TrophyChange}" : last.TrophyChange.ToString()),
                t, favIcon);
        }
    }

    /// <summary>
    /// Адрес картинки для карточки. null, если публичный адрес не настроен: тогда
    /// вызывающий отдаст текстовый вариант. Ссылка в никуда хуже отсутствия картинки —
    /// Telegram молча выбросит такой результат из выдачи, и это невозможно отладить.
    /// </summary>
    private string? Img(string kind, string tag)
    {
        if (PublicBaseUrl is not { } baseUrl) return null;

        // Метка десятиминутного окна в адресе. Telegram кэширует скачанную картинку
        // по URL и держит её заметно дольше, чем живут наши данные, — из-за этого
        // в чате неделями показывалась бы одна и та же старая карточка. Меняющийся
        // адрес заставляет его перекачать, но не чаще раза в десять минут: постоянно
        // новый URL сводил бы кэш на нет и заставлял перерисовывать на каждый показ.
        var bucket = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute / 10;
        return $"{baseUrl}/api/img/{kind}/{tag.TrimStart('#')}.jpg?v={bucket}";
    }

    /// <summary>Карточка картинкой, а если картинки нет — та же карточка текстом.</summary>
    private InlineQueryResult Photo(
        string id, string? img, string title, string desc, string text, BotText t, string? thumb) =>
        img is null
            ? Card(id, title, desc, text, t, thumb)
            : new InlineQueryResultPhoto(id, img, img)
            {
                Title = title,
                Description = desc,
                Caption = text + t.InlineFooter,
                // Размеры подсказывают Telegram, как показать превью, не дожидаясь загрузки
                PhotoWidth = CardWidth,
                PhotoHeight = CardHeight,
                ReplyMarkup = OpenBotKeyboard(t),
            };

    /// <summary>
    /// Размеры карточек — те же, что рисует API (см. CardRenderer.StatHeight/DeckHeight).
    /// Разойдутся — Telegram отведёт под превью коробку не того размера, и картинка
    /// либо обрежется, либо повиснет в серой рамке.
    /// </summary>
    private const int CardWidth = 800;
    private const int CardHeight = 360;
    private const int DeckCardHeight = 540;

    private InlineQueryResultArticle Card(
        string id, string title, string desc, string text, BotText t, string? thumb) =>
        new(id, title, PlainText(text + t.InlineFooter))
        {
            Description = desc,
            ThumbnailUrl = thumb,
            ReplyMarkup = OpenBotKeyboard(t),
        };

    /// <summary>
    /// Ссылка «открыть колоду в игре» для текущей колоды игрока. null, если хотя бы
    /// одной карты нет в справочнике: неполная ссылка открыла бы не ту колоду.
    /// </summary>
    private static string? DeckLink(
        List<CrDeckCard> deck, IReadOnlyDictionary<string, CrCatalogCard>? catalog)
    {
        if (catalog is null || deck.Count == 0) return null;

        var ids = new List<int>(deck.Count);
        foreach (var c in deck)
        {
            if (!catalog.TryGetValue(c.Name, out var card) || card.Id <= 0) return null;
            ids.Add(card.Id);
        }
        return $"https://link.clashroyale.com/deck/en?deck={string.Join(';', ids)}";
    }

    /// <summary>
    /// Текст без предпросмотра ссылок: карточка должна выглядеть карточкой,
    /// а не постом с развёрнутой плашкой на пол-экрана.
    /// </summary>
    private static InputTextMessageContent PlainText(string text) =>
        new(text) { LinkPreviewOptions = new LinkPreviewOptions { IsDisabled = true } };

    private InlineKeyboardMarkup OpenBotKeyboard(BotText t) =>
        new(InlineKeyboardButton.WithUrl(t.InlineOpenBot, $"https://t.me/{_botUsername}"));

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
            if (msg.Chat.Type == ChatType.Private) await UnblockDmAsync(msg, sp, ct);

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
                        // Приглашение от лидера: /start claim_<подписанный код>. Отвечает само
                        // за себя целиком, обычное приветствие после него было бы шумом.
                        if (arg is not null && arg.StartsWith(ClaimLink.Prefix, StringComparison.Ordinal))
                        {
                            await HandleClaimAsync(msg, arg, sp, t, ct);
                            return;
                        }

                        // Откуда пришёл: /start ad_<код> — реклама, /start ref_<id> — реферал.
                        // В базу, а не в память воркера: память терялась при каждом деплое,
                        // а приложение, где тоже привязывают тег, её не видело вовсе.
                        try { await sp.GetRequiredService<TrackStartUseCase>().ExecuteAsync(msg.From!.Id, arg, ct); }
                        catch (Exception ex) { logger.LogWarning(ex, "Could not record start source {Arg}", arg); }

                        // Пришёл по кнопке «включить в личке» из группы - сразу трекер, а не приветствие
                        if (arg == TrackerStartArg && await SendTrackerStatusAsync(msg, sp, ct)) return;

                        // Кнопка приложения прямо под приветствием: раньше её не было, и
                        // человек, не догадавшийся про кнопку меню, приложения не видел.
                        await bot.SendMessage(msg.Chat.Id, t.StartPrivate,
                            replyMarkup: AppButton(t.OpenAppButton), cancellationToken: ct);
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
                    // Главный шаг воронки: клан подключён. Засчитывается тому, кто
                    // подключил, — если он пришёл по рекламе или реферальной ссылке.
                    if (clanName is not null)
                    {
                        try { await sp.GetRequiredService<IAcquisitionRepository>().MarkClanConnectedAsync(msg.From!.Id, ct); }
                        catch (Exception ex) { logger.LogWarning(ex, "Could not mark clan connected"); }
                    }
                    var topicNote = msg.MessageThreadId is not null ? t.SetupTopicNote : "";
                    await Reply(msg, clanName is null
                        ? t.SetupClanNotFound
                        : string.Format(t.SetupOk, clanName) + topicNote, ct);
                    break;

                case "/link":
                    if (arg is null) { await Reply(msg, t.LinkFormat, ct); return; }
                    var isPrivate = msg.Chat.Type == ChatType.Private;
                    var linkChatId = isPrivate ? (long?)null : msg.Chat.Id;
                    // Пригласившего use case берёт из базы сам
                    var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
                        .ExecuteAsync(msg.From!.Id, arg, linkChatId, null, msg.From!.Username, ct);
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
                    var nudgeResult = await sp.GetRequiredService<NudgePlayersUseCase>()
                        .ExecuteAsync(nudgeClan.Id, ct);
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

                case "/help":
                case "/помощь":
                    await Reply(msg, t.HelpText, ct);
                    break;

                case "/me":
                case "/я":
                    if (msg.Chat.Type != ChatType.Private) { await Reply(msg, t.HelpText, ct); return; }
                    await SendMyReviewAsync(msg.Chat.Id, msg.From!.Id, null, sp, t, ct);
                    break;

                case "/deck":
                case "/колода":
                    await SendMyDeckAsync(msg, sp, t, ct);
                    break;

                case "/meta":
                case "/мета":
                    await SendMetaAsync(msg, sp, t, ct);
                    break;

                case "/plus":
                case "/плюс":
                    await SendPlusInfoAsync(msg, sp, t, ct);
                    break;

                case "/tracker":
                case "/трекер":
                {
                    if (msg.Chat.Type != ChatType.Private)
                    {
                        // Трекер пишет только в личку. В группе - кнопка туда, а не справка:
                        // человек нажал /tracker из анонса и должен дойти до включения в один тап.
                        await bot.SendMessage(msg.Chat.Id, t.TrkInGroup,
                            messageThreadId: msg.MessageThreadId,
                            replyParameters: msg.MessageId,
                            replyMarkup: _botUsername == "bot" ? null
                                : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(t.TrkBtnDm,
                                    $"https://t.me/{_botUsername}?start={TrackerStartArg}")),
                            cancellationToken: ct);
                        return;
                    }
                    if (!await SendTrackerStatusAsync(msg, sp, ct)) await Reply(msg, t.NotLinkedYet, ct);
                    break;
                }

                case "/paysupport":
                    var owner = config["Owner:Username"]?.Trim().TrimStart('@');
                    await Reply(msg, string.Format(t.PaySupport,
                        string.IsNullOrEmpty(owner) ? t.PaySupportOwnerFallback : "@" + owner), ct);
                    break;

                case "/terms":
                    await Reply(msg, t.Terms, ct);
                    break;

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

                default:
                    // В личке непонятое сообщение - повод показать, что бот умеет. В группе
                    // молчим: там пишут друг другу, а не боту.
                    if (msg.Chat.Type == ChatType.Private) await Reply(msg, t.HelpText, ct);
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
                // Текст подробностей больше не подставляем: игрок получал в ответ
                // дамп исключения Postgres, из которого ему нечего извлечь. Полная
                // ошибка уже записана логом строкой выше.
                Microsoft.EntityFrameworkCore.DbUpdateException or System.Data.Common.DbException =>
                    errText.ErrDb,
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

        // Уже привязан к другому тегу — не перепривязываем молча. Раньше любой
        // присланный тег (друга, соперника) тихо уводил привязку на себя, а с
        // платным Плюсом это ещё и «потеря» оплаченного доступа на глазах у человека.
        var current = await sp.GetRequiredService<IPlayerRepository>().GetByTelegramIdAsync(msg.From!.Id, ct);
        if (current is not null && !string.Equals(current.PlayerTag, tag, StringComparison.OrdinalIgnoreCase))
        {
            await ShowForeignTagAsync(msg, tag, current.PlayerTag, sp, t, ct);
            return;
        }

        // Привязываем игрока (без клана — из ЛС)
        var playerName = await sp.GetRequiredService<LinkPlayerUseCase>()
            .ExecuteAsync(msg.From!.Id, tag, null, null, msg.From!.Username, ct);

        if (playerName is null)
        {
            await Reply(msg, string.Format(t.QuickNotFound, tag), ct);
            return;
        }

        var crApi = sp.GetRequiredService<IClashRoyaleApi>();
        string? clanTag = null;
        try { clanTag = await crApi.GetPlayerClanTagAsync(tag, ct); }
        catch { /* not critical */ }

        // Сначала — то, ради чего человек пришёл: его собственные бои. Клан для этого
        // не нужен; война, если она есть, придёт следующим сообщением.
        await SendMyReviewAsync(msg.Chat.Id, msg.From!.Id, string.Format(t.LinkedHead, playerName), sp, t, ct,
            noClan: clanTag is null);

        if (clanTag is null) return;

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
                // TelegramChatId = 0 означает «чат не привязан»: клан заведён по тегу
                // игрока, а не командой /setup. Уникальный индекс по чату поэтому
                // частичный, иначе второй такой клан падал бы с duplicate key.
                existingClan = new Clan
                {
                    ClanTag = clanTag,
                    Name = autoName,
                    TelegramChatId = 0,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                try
                {
                    await clanRepo.AddAsync(existingClan, ct);
                    await clanRepo.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Гонка: два игрока одного клана прислали теги одновременно, оба
                    // увидели «клана нет» и оба вставили. Проигравший просто берёт
                    // чужую запись — она ничем не хуже своей.
                    existingClan = await clanRepo.GetByTagAsync(clanTag, ct);
                }
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

        // Войны нет или она недоступна — разбор уже отправлен, дублировать «привязан» незачем
        if (status is null) return;

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

    /// <summary>
    /// Короткий разбор боёв сообщением: процент побед, худшая карта, тильт — и кнопка
    /// в приложение за полным. Им же отвечает /me, и его же присылает бот сразу после
    /// привязки: человек видит пользу в первом же ответе, а не «привязан, открой меню».
    /// </summary>
    private async Task SendMyReviewAsync(
        long chatId, long telegramUserId, string? header, IServiceProvider sp, BotText t, CancellationToken ct,
        bool noClan = false)
    {
        var analysis = await sp.GetRequiredService<GetBattleAnalysisUseCase>().ExecuteAsync(telegramUserId, 0, ct);
        if (analysis is null)
        {
            await bot.SendMessage(chatId, t.NotLinkedYet, cancellationToken: ct);
            return;
        }

        if (header is null)
        {
            var me = await sp.GetRequiredService<IPlayerRepository>().GetByTelegramIdAsync(telegramUserId, ct);
            header = $"👤 {me?.Name ?? analysis.PlayerTag} · {analysis.PlayerTag}";
        }

        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine();
        if (analysis.Games > 0)
        {
            sb.AppendLine(string.Format(t.LinkedStats, analysis.Games, analysis.WinPercent.ToString("0"),
                analysis.Wins, analysis.Losses));
            if (analysis.ToughCards.FirstOrDefault() is { } tough)
                sb.AppendLine(string.Format(t.LinkedTough, tough.Card.Name, tough.WinPercent.ToString("0"), tough.Games));
            if (analysis.Tilt is { AfterTwoLossesGames: >= 4 } tilt)
                sb.AppendLine(string.Format(t.LinkedTilt, tilt.AfterTwoLossesWinPercent.ToString("0")));
        }
        else
        {
            sb.AppendLine(t.LinkedNoBattles);
        }

        if (noClan)
        {
            sb.AppendLine();
            sb.AppendLine(t.LinkedNoClanLine);
        }
        sb.AppendLine();
        sb.Append(t.LinkedTail);

        await bot.SendMessage(chatId, sb.ToString(),
            replyMarkup: AppButton(t.OpenReviewButton, "review"), cancellationToken: ct);
    }

    /// <summary>
    /// Прислали чужой тег, будучи уже привязанным: показываем, кто это, и спрашиваем,
    /// не свой ли это второй аккаунт. Перепривязка — только по кнопке.
    /// </summary>
    private async Task ShowForeignTagAsync(
        Message msg, string tag, string currentTag, IServiceProvider sp, BotText t, CancellationToken ct)
    {
        var info = await sp.GetRequiredService<IClashRoyaleApi>().GetPlayerInfoAsync(tag, ct);
        if (info is null)
        {
            await Reply(msg, string.Format(t.QuickNotFound, tag), ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(t.ForeignTagHead, info.Name, info.Tag));
        var line = $"🏆 {info.Trophies:N0}";
        if (info.CurrentPathOfLegend is { Trophies: > 0 } pol) line += $" · 🏅 {pol.Trophies:N0}";
        sb.AppendLine(line);
        if (!string.IsNullOrEmpty(info.ClanName)) sb.AppendLine($"🛡 {info.ClanName}");
        sb.AppendLine();
        sb.Append(string.Format(t.ForeignTagLinkedAs, currentTag));

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData(t.ForeignTagRelinkButton, "relink|" + info.Tag) },
        };
        if (_botUsername != "bot")
            rows.Add(new[] { InlineKeyboardButton.WithUrl(t.OpenAppButton, $"https://t.me/{_botUsername}?startapp") });

        await bot.SendMessage(msg.Chat.Id, sb.ToString(), replyParameters: msg.MessageId,
            replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
    }

    /// <summary>Кнопки под сообщениями бота. Пока одна: «Это мой аккаунт».</summary>
    private async Task ProcessCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var data = callback.Data ?? "";

        if (data.StartsWith("tilt|", StringComparison.Ordinal) || data.StartsWith("tintro|", StringComparison.Ordinal))
        {
            var result = await sp.GetRequiredService<TiltActionsUseCase>()
                .HandleAsync(callback.From.Id, data, callback.From.LanguageCode, ct);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            if (result is null || callback.Message is not { } message) return;
            try
            {
                // Ответ там же, где нажали: без кнопок, чтобы второй раз не нажать
                await bot.EditMessageText(message.Chat.Id, message.MessageId, result.Text,
                    replyMarkup: new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>()), cancellationToken: ct);
            }
            catch (Exception ex) { logger.LogDebug(ex, "Could not edit tilt message"); }
            return;
        }

        if (data.StartsWith("trk|", StringComparison.Ordinal))
        {
            var result = await sp.GetRequiredService<TrackerActionsUseCase>()
                .HandleAsync(callback.From.Id, data, callback.From.LanguageCode, ct);
            if (result is { Toast: true })
            {
                await bot.AnswerCallbackQuery(callback.Id, text: result.Text, showAlert: true, cancellationToken: ct);
                return;
            }
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            if (result is null || callback.Message is not { } message) return;
            // Правка того же сообщения, без нового - тихо, как и весь трекер
            await sp.GetRequiredService<INotificationSender>()
                .EditUserMessageAsync(message.Chat.Id, message.MessageId, result.Text, result.Rows, ct);
            return;
        }

        if (data.StartsWith("relink|", StringComparison.Ordinal))
        {
            var tag = data["relink|".Length..];
            var t = await TextForUserAsync(callback.From.Id, callback.From.LanguageCode, sp, ct);
            var name = await sp.GetRequiredService<LinkPlayerUseCase>()
                .ExecuteAsync(callback.From.Id, tag, null, null, callback.From.Username, ct);

            await bot.AnswerCallbackQuery(callback.Id, text: name is null ? t.RelinkFailed : null,
                cancellationToken: ct);
            if (name is null) return;

            var chatId = callback.Message?.Chat.Id ?? callback.From.Id;
            await SendMyReviewAsync(chatId, callback.From.Id, string.Format(t.RelinkDone, name), sp, t, ct);
            return;
        }

        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
    }

    /// <summary>/deck — колода из последнего боя и кнопка, открывающая её прямо в игре.</summary>
    private async Task SendMyDeckAsync(Message msg, IServiceProvider sp, BotText t, CancellationToken ct)
    {
        var me = await sp.GetRequiredService<IPlayerRepository>().GetByTelegramIdAsync(msg.From!.Id, ct);
        if (me is null) { await Reply(msg, t.NotLinkedYet, ct); return; }

        var crApi = sp.GetRequiredService<IClashRoyaleApi>();
        var log = await crApi.GetRecentBattlesAsync(me.PlayerTag, ct);
        var deck = log.Where(CollectPlayerBattlesUseCase.Counts)
            .OrderByDescending(b => b.BattleTimeUtc)
            .Select(b => b.MyDeck)
            .FirstOrDefault();
        if (deck is null) { await Reply(msg, t.DeckNone, ct); return; }

        var catalog = await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
        var elixir = deck.Average(c => catalog.GetValueOrDefault(c.Id)?.ElixirCost ?? 0);
        var names = string.Join("\n", deck.Select(c => (c.EvolutionLevel > 0 ? "✨ " : "• ") + c.Name));
        var link = GetMetaDecksUseCase.CopyLink(deck.Select(MetaCard.Key).ToArray());

        await bot.SendMessage(msg.Chat.Id, string.Format(t.DeckHead, elixir.ToString("0.0"), names),
            replyParameters: msg.MessageId,
            replyMarkup: link is null
                ? AppButton(t.OpenAppButton)
                : new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(t.DeckOpenButton, link)),
            cancellationToken: ct);
    }

    /// <summary>/meta — пять лучших колод топа за неделю, по боям.</summary>
    private async Task SendMetaAsync(Message msg, IServiceProvider sp, BotText t, CancellationToken ct)
    {
        var meta = await sp.GetRequiredService<GetMetaDecksUseCase>().ExecuteAsync(ct);
        if (meta is null || meta.Decks.Count == 0) { await Reply(msg, t.MetaEmpty, ct); return; }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(t.MetaHead, meta.Battles.ToString("N0")));
        var place = 0;
        foreach (var d in meta.Decks.Take(5))
        {
            place++;
            sb.AppendLine();
            sb.AppendLine(string.Format(t.MetaRow, place, d.WinPercent.ToString("0.#"), d.Games,
                string.Join(" · ", d.Cards.Select(c => (c.Evo ? "✨" : "") + c.Name))));
        }

        await bot.SendMessage(msg.Chat.Id, sb.ToString().TrimEnd(), replyParameters: msg.MessageId,
            replyMarkup: AppButton(t.OpenAppButton, "meta"), cancellationToken: ct);
    }

    /// <summary>/plus — что даёт Плюс и есть ли он у тебя. Купить — в приложении, одним нажатием.</summary>
    private async Task SendPlusInfoAsync(Message msg, IServiceProvider sp, BotText t, CancellationToken ct)
    {
        var status = await sp.GetRequiredService<PlusAccess>().GetAsync(msg.From!.Id, ct);
        var offer = await PlusSales.ReadAsync(sp.GetRequiredService<IServiceSettingRepository>(), ct);

        var line = !status.Paywall ? t.PlusFreeLine
            : status.Active && status.Until is { } until ? string.Format(t.PlusActiveLine, until.ToString("dd.MM.yyyy"))
            : string.Format(t.PlusOfferLine, offer.Price7, offer.Price30);

        await bot.SendMessage(msg.Chat.Id, string.Format(t.PlusInfo, line), replyParameters: msg.MessageId,
            replyMarkup: AppButton(t.PlusButton, "plus"), cancellationToken: ct);
    }

    /// <summary>
    /// Меню команд в личке. Ставится при каждом запуске: так список не расходится
    /// с тем, что бот умеет на самом деле, а правка не требует похода в BotFather.
    /// Группы не трогаем: там свои команды клана.
    /// </summary>
    private async Task RegisterPrivateCommandsAsync(CancellationToken ct)
    {
        var sets = new List<(string? Lang, string[][] Items)>
        {
            (null, new[]
            {
                new[] { "me", "Короткий разбор твоих боёв" }, new[] { "deck", "Твоя колода из последнего боя" },
                new[] { "meta", "Лучшие колоды топа за неделю" }, new[] { "tracker", "Трекер боёв: разбор после каждого боя" },
                new[] { "plus", "Clanify Плюс" },
                new[] { "help", "Что умеет бот" }, new[] { "paysupport", "Помощь с оплатой" }, new[] { "terms", "Условия" },
            }),
            ("uk", new[]
            {
                new[] { "me", "Короткий розбір твоїх боїв" }, new[] { "deck", "Твоя колода з останнього бою" },
                new[] { "meta", "Найкращі колоди топу за тиждень" }, new[] { "tracker", "Трекер боїв: розбір після кожного бою" },
                new[] { "plus", "Clanify Плюс" },
                new[] { "help", "Що вміє бот" }, new[] { "paysupport", "Допомога з оплатою" }, new[] { "terms", "Умови" },
            }),
            ("en", new[]
            {
                new[] { "me", "A short review of your battles" }, new[] { "deck", "Your deck from the last battle" },
                new[] { "meta", "The top's best decks this week" }, new[] { "tracker", "Battle tracker: a breakdown after every battle" },
                new[] { "plus", "Clanify Plus" },
                new[] { "help", "What the bot can do" }, new[] { "paysupport", "Payment help" }, new[] { "terms", "Terms" },
            }),
        };

        foreach (var (lang, items) in sets)
        {
            try
            {
                await bot.SetMyCommands(
                    items.Select(i => new BotCommand { Command = i[0], Description = i[1] }),
                    BotCommandScope.AllPrivateChats(), lang, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not set private commands for {Lang}", lang ?? "default");
            }
        }
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

    /// <summary>
    /// Похоже на тег Clash Royale. Теги пишутся только буквами 0289PYLQGRJCUV, так что
    /// «привет» или «hello» больше не уходят в поиск игрока (а раньше уходили и
    /// получали в ответ «игрок не найден» вместо справки).
    /// </summary>
    /// <summary>
    /// Человек сам написал боту - значит, писать ему в личку можно. Если раньше доставка
    /// не удалась (он не запускал бота), «Стоп-тильт» снова может до него достучаться.
    /// </summary>
    private static async Task UnblockDmAsync(Message msg, IServiceProvider sp, CancellationToken ct)
    {
        if (msg.From is null) return;
        try
        {
            var prefsRepo = sp.GetRequiredService<IPlayerAlertPrefsRepository>();
            var prefs = await prefsRepo.GetAsync(msg.From.Id, ct);
            if (prefs is { DmBlocked: true })
            {
                prefs.DmBlocked = false;
                await prefsRepo.SaveChangesAsync(ct);
            }
        }
        catch { /* не критично — обработка сообщения важнее */ }
    }

    private static bool IsLikelyCrTag(string text)
    {
        var t = text.Trim().ToUpperInvariant();
        if (t.StartsWith('#')) t = t[1..];
        return t.Length is >= 3 and <= 12 && t.All(c => TagAlphabet.Contains(c));
    }

    private const string TagAlphabet = "0289PYLQGRJCUV";

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
    /// Приглашение по ссылке: /start claim_&lt;код&gt; в личке.
    ///
    /// Ради этого всё и затевалось — человеку не нужен @username и не нужно ничего
    /// писать в чате, достаточно нажать на ссылку. Заодно он нажимает «Старт», и бот
    /// получает право писать ему в личку, чего при привязке лидером не бывает.
    /// </summary>
    private async Task HandleClaimAsync(Message msg, string arg, IServiceProvider sp, BotText t, CancellationToken ct)
    {
        var secret = ClanWarTracker.Infrastructure.DependencyInjection.CleanToken(
            Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
            ?? config["TELEGRAM_BOT_TOKEN"]
            ?? config["Telegram:BotToken"]);

        var payload = string.IsNullOrEmpty(secret) ? null : ClaimLink.Verify(arg, secret, DateTime.UtcNow);
        if (payload is null) { await Reply(msg, t.ClaimBadLink, ct); return; }

        var from = msg.From!;
        var clanPlayers = await sp.GetRequiredService<IPlayerRepository>()
            .GetByClanIdAsync(payload.ClanId, ct);
        var existing = clanPlayers.FirstOrDefault(p =>
            string.Equals(p.PlayerTag, payload.PlayerTag, StringComparison.OrdinalIgnoreCase));

        // Тег уже за другим аккаунтом — значит, ссылку либо переслали дальше, либо ей
        // воспользовались раньше. Это и делает приглашение одноразовым: отдельного
        // счётчика использований нет, роль отметки «использовано» играет сама привязка.
        if (existing?.TelegramUserId is long owner && owner != from.Id)
        {
            await Reply(msg, t.ClaimTaken, ct);
            return;
        }

        var result = await sp.GetRequiredService<BindPlayerUseCase>()
            .BindAsync(payload.ClanId, payload.PlayerTag, from.Username, from.Id, ct);

        await Reply(msg, result.Outcome switch
        {
            BindOutcome.TagNotFound => t.BindTagNotFound,
            BindOutcome.NotInClan => t.BindNotInClan,
            _ => string.Format(t.ClaimOk, result.PlayerName),
        }, ct);
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

        // Владелец настраивает бота в любом чате, не выпрашивая админку. Его знаем
        // из конфига, поэтому отвечаем до кэша и до похода в Telegram.
        if (ownerAccess.IsOwner(msg.From!.Id)) return true;

        var key = (msg.Chat.Id, msg.From!.Id);
        if (_adminCache.TryGetValue(key, out var hit) && hit.Until > DateTime.UtcNow)
            return hit.IsAdmin;

        // То же право можно выдать и модератору — отдельной галочкой, не «за компанию»
        // с доступом к панели. Проверяем после кэша: это поход в базу, а команды в
        // группах сыплются пачками.
        if (await HasChatAdminGrantAsync(msg.From.Id, msg.From.Username, ct))
        {
            _adminCache[key] = (true, DateTime.UtcNow.AddMinutes(5));
            return true;
        }

        var member = await bot.GetChatMember(msg.Chat.Id, msg.From.Id, ct);
        var isAdmin = member.Status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator;
        _adminCache[key] = (isAdmin, DateTime.UtcNow.AddMinutes(5));
        return isAdmin;
    }

    /// <summary>
    /// Выдано ли этому человеку право настраивать бота в любом чате.
    ///
    /// Бот — фоновая служба, а права лежат в базе за scoped-репозиторием, поэтому
    /// открываем область вручную. Ошибку глотаем: недоступная база не должна
    /// превращаться в «бот перестал отвечать на команды» — просто останется
    /// обычная проверка админки чата.
    /// </summary>
    private async Task<bool> HasChatAdminGrantAsync(long userId, string? username, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var access = scope.ServiceProvider.GetRequiredService<ServiceAccess>();
            var me = await access.ResolveAsync(userId, username, ct);
            return me.Can(ServicePermission.ChatAdmin);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось проверить права модератора для {UserId}", userId);
            return false;
        }
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
    /// <summary>Язык для лички по человеку, а не по сообщению — для кнопок, у которых сообщения нет.</summary>
    private static async Task<BotText> TextForUserAsync(long userId, string? languageCode, IServiceProvider sp, CancellationToken ct)
    {
        try
        {
            var player = await sp.GetRequiredService<IPlayerRepository>().GetByTelegramIdAsync(userId, ct);
            if (player?.ClanId is int clanId
                && await sp.GetRequiredService<IClanRepository>().GetByIdAsync(clanId, ct) is { } clan)
                return NotificationSettings.Parse(clan.NotificationSettingsJson).Text;
        }
        catch { /* язык — не повод не ответить */ }
        return BotText.For(languageCode);
    }

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

    /// <summary>
    /// Бота добавили в группу — здороваемся и объясняем, что делать.
    ///
    /// Момент добавления единственный, когда на бота точно смотрят. Промолчать здесь
    /// значит потерять клан: дальше сообщение утонет в чате, а команду /start в группе
    /// никто не пишет.
    /// </summary>
    private async Task ProcessMembershipAsync(ChatMemberUpdated m, CancellationToken ct)
    {
        // Только группы и только переход «не был участником → стал».
        if (m.Chat.Type is not (ChatType.Group or ChatType.Supergroup)) return;

        var was = m.OldChatMember.Status;
        var now = m.NewChatMember.Status;
        var joined = was is ChatMemberStatus.Left or ChatMemberStatus.Kicked
                     && now is ChatMemberStatus.Member or ChatMemberStatus.Administrator;
        if (!joined) return;

        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var clans = sp.GetRequiredService<IClanRepository>();
        var clan = await clans.GetByChatIdAsync(m.Chat.Id, ct);
        var t = NotificationSettings.Parse(clan?.NotificationSettingsJson).Text;

        // Клан мог быть привязан раньше — тогда бота вернули, а не привели впервые.
        var text = clan is null ? t.GroupJoined : string.Format(t.GroupJoinedReady, clan.Name);
        await SendWithAppButtonAsync(m.Chat.Id, text, ct);
    }

    /// <summary>Кнопка «Открыть приложение», если username бота известен.</summary>
    private InlineKeyboardMarkup? AppButton(string label) => AppButton(label, null);

    /// <summary>Параметр /start из кнопки «включить в личке»: открывает трекер сразу.</summary>
    private const string TrackerStartArg = "tracker";

    /// <summary>Состояние трекера с кнопками. false - игрок не привязан.</summary>
    private static async Task<bool> SendTrackerStatusAsync(Message msg, IServiceProvider sp, CancellationToken ct)
    {
        var tracker = await sp.GetRequiredService<TrackerActionsUseCase>()
            .StatusAsync(msg.From!.Id, msg.From.LanguageCode, ct);
        if (tracker is null) return false;
        await sp.GetRequiredService<INotificationSender>()
            .SendDmAsync(msg.Chat.Id, tracker.Text, tracker.Rows, silent: false, ct);
        return true;
    }

    /// <summary>
    /// Кнопка приложения с параметром запуска: «review» открывает разбор, «plus» —
    /// Плюс, «meta» — мету. Приложение читает его из start_param.
    /// </summary>
    private InlineKeyboardMarkup? AppButton(string label, string? startParam) =>
        _botUsername == "bot"
            ? null
            : new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(label,
                    $"https://t.me/{_botUsername}?startapp" + (startParam is null ? "" : "=" + startParam)));

    private Task SendWithAppButtonAsync(long chatId, string text, CancellationToken ct) =>
        bot.SendMessage(chatId, text, replyMarkup: AppButton("🎮 Открыть приложение"),
            cancellationToken: ct);

    /// <summary>
    /// Ответ на команду. Кнопка приложения идёт под каждым ответом: раньше их не было
    /// вовсе, и игрок, прочитав «привяжи себя», не имел ни одного очевидного способа
    /// это сделать — текст предлагал уйти в личку и вручную набрать тег.
    /// </summary>
    private Task Reply(Message msg, string text, CancellationToken ct) =>
        bot.SendMessage(msg.Chat.Id, text, replyParameters: msg.MessageId,
            replyMarkup: AppButton("🎮 Открыть приложение"), cancellationToken: ct);
}
