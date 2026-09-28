using ClanWarTracker.Application.Security;
using ClanWarTracker.Application.UseCases;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Enums;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace ClanWarTracker.Api.Controllers;

/// <summary>
/// Панель сервиса: кланы, сводка, тарифы, рассылка, модераторы.
///
/// Два уровня доступа. Владелец (Owner:TelegramUserId) может всё. Модератор видит
/// то же, что владелец, но ничего не меняет: рассылка уходит двум сотням человек и
/// не отзывается, выданный тариф стоит денег, удаление клана уносит историю войн.
/// Поэтому право смотреть и право менять разделены явно, а не «по совести».
/// </summary>
[ApiController]
[Route("api/owner")]
public class OwnerController(
    IClanRepository clans,
    GetOwnerDashboardUseCase dashboard,
    GetOwnerClanDetailUseCase clanDetail,
    OwnerBroadcastUseCase broadcast,
    HarvestTopPlayersUseCase harvestTop,
    ITopPlayerRepository topPlayers,
    ISponsorPaymentRepository sponsorPayments,
    ICampaignRepository campaigns,
    GetCampaignFunnelUseCase campaignFunnel,
    IServiceModeratorRepository moderators,
    IPlayerRepository players,
    IServiceSettingRepository settings,
    ServiceAccess access,
    IServiceScopeFactory scopes,
    ITelegramBotClient bot,
    PlusAccess plusAccess,
    IEntitlementRepository entitlements,
    RevokePurchaseUseCase revokePurchase,
    ITiltAlertRepository tiltAlerts,
    IPlayerAlertPrefsRepository alertPrefs,
    ISentNotificationRepository sentLog,
    ChallengeUseCase challenge,
    IChallengeRepository challengeEntries,
    ILogger<OwnerController> logger) : ControllerBase
{
    public record ChallengeRequest(string? Title, string? Prize, DateTime StartUtc, DateTime EndUtc);

    /// <summary>GET /api/owner/challenge — текущий челлендж и сколько вступило.</summary>
    [HttpGet("challenge")]
    public async Task<IActionResult> GetChallenge(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.AppSettings, ct) is { } deny) return deny;
        var e = await challenge.CurrentAsync(ct);
        var count = (await challengeEntries.GetEntriesAsync(e.Id, ct)).Count;
        return Ok(new { e.Id, e.Title, e.Prize, e.StartUtc, e.EndUtc, status = e.Status(DateTime.UtcNow), participants = count });
    }

    /// <summary>
    /// POST /api/owner/challenge — название, приз и время. Id события - по дате старта:
    /// сдвинул выходные - это новое событие с новой таблицей.
    /// </summary>
    [HttpPost("challenge")]
    public async Task<IActionResult> SetChallenge([FromBody] ChallengeRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.AppSettings, ct) is { } deny) return deny;
        var start = DateTime.SpecifyKind(req.StartUtc.ToUniversalTime(), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(req.EndUtc.ToUniversalTime(), DateTimeKind.Utc);
        if (end <= start || end - start > TimeSpan.FromDays(14)) return BadRequest(new { error = "bad_dates" });
        var title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim()[..Math.Min(60, req.Title.Trim().Length)];
        var prize = string.IsNullOrWhiteSpace(req.Prize) ? null : req.Prize.Trim()[..Math.Min(60, req.Prize.Trim().Length)];
        var e = new ChallengeUseCase.Event($"ch-{start:yyyyMMddHHmm}", title, prize, start, end);
        await challenge.SaveAsync(e, ct);
        return Ok(new { e.Id, e.Title, e.Prize, e.StartUtc, e.EndUtc, status = e.Status(DateTime.UtcNow) });
    }

    public record PlusSettingsRequest(bool Paywall, int Price7, int Price30);
    public record TrackerSettingsRequest(bool Dm, string? Beta);
    public record GrantPlusRequest(string PlayerTag, int Days);
    public record RefundRequest(string ChargeId);

    /// <summary>1 — сбор из панели уже идёт. Второй параллельный удвоил бы запросы к API игры.</summary>
    private static int _harvestRunning;

    public record BroadcastRequest(string Text, string Target);
    public record AddModeratorRequest(string Username, string? Note, string[]? Permissions);
    public record GrantSponsorRequest(string PlayerTag, int Days);
    public record TabsRequest(string[] Tabs);
    public record SetPermissionsRequest(string[] Permissions);

    /// <summary>GET /api/owner/me — кто я для сервиса. Фронт по этому решает, показывать ли панель.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        return Ok(new
        {
            role = me.Role.ToString().ToLowerInvariant(),
            // Списком строк, а не числом: фронту нужно рисовать галочки, и число
            // пришлось бы разбирать у него второй, отдельной копией правил.
            permissions = PermissionNames(me.Permissions),
        });
    }

    /// <summary>GET /api/owner/clans — кланы сервиса: тариф, активность, привязки.</summary>
    [HttpGet("clans")]
    public async Task<IActionResult> GetClans(CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;
        return Ok(await dashboard.GetClansAsync(ct));
    }

    /// <summary>
    /// GET /api/owner/clans/{id} — детали клана: привязанные игроки с @username и ролями
    /// (главы первыми — с ними имеет смысл говорить о клане).
    /// </summary>
    [HttpGet("clans/{id:int}")]
    public async Task<IActionResult> GetClanDetail(int id, CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;

        var detail = await clanDetail.ExecuteAsync(id, ct);
        return detail is null ? NotFound(new { error = "clan_not_found" }) : Ok(detail);
    }

    /// <summary>GET /api/owner/stats — детальная сводка по сервису.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;
        return Ok(await dashboard.GetStatsAsync(ct));
    }

    /// <summary>
    /// POST /api/owner/broadcast — ручная рассылка. Body: { text, target: "dm"|"chats"|"both" }.
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Broadcast, ct) is { } deny) return deny;

        var text = req.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return BadRequest(new { error = "empty_text" });
        if (text.Length > 4000) return BadRequest(new { error = "too_long", message = "Макс 4000 символов" });

        var target = req.Target?.ToLowerInvariant();
        var toDm = target is "dm" or "both";
        var toChats = target is "chats" or "both";
        if (!toDm && !toChats) return BadRequest(new { error = "bad_target", message = "target: dm | chats | both" });

        var r = await broadcast.ExecuteAsync(text, toDm, toChats, ct);
        return Ok(new { sentDm = r.SentDm, sentChats = r.SentChats, failedDm = r.FailedDm, failedChats = r.FailedChats });
    }

    /// <summary>
    /// DELETE /api/owner/clans/{id} — отвязать клан от сервиса.
    /// Удаляет клан вместе с привязками игроков и историей войн.
    /// </summary>
    [HttpDelete("clans/{id:int}")]
    public async Task<IActionResult> DeleteClan(int id, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.DeleteClans, ct) is { } deny) return deny;

        var clan = (await clans.GetAllAsync(ct)).FirstOrDefault(c => c.Id == id);
        if (clan is null) return NotFound(new { error = "clan_not_found" });

        await clans.RemoveAsync(clan, ct);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// POST /api/owner/top/harvest — собрать снимок мирового топа прямо сейчас.
    ///
    /// Сбор живёт в суточном цикле воркера, и до этой ручки единственным способом
    /// проверить, работает ли он, было ждать следующего тика и идти читать логи.
    /// Когда снимок не собирается, проверять приходится часто, а ждать — некогда.
    /// </summary>
    [HttpPost("top/harvest")]
    public async Task<IActionResult> HarvestTop(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Maintenance, ct) is { } deny) return deny;

        // Сбор идёт минуты, а панель ждёт ответа секунды: раньше запрос обрывался
        // по таймауту клиента, отмена доходила до сбора и убивала его на полпути.
        // Теперь кнопка только запускает, итог панель читает из /top/status.
        if (Interlocked.CompareExchange(ref _harvestRunning, 1, 0) != 0)
            return Accepted(new { started = false, running = true });

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var harvest = scope.ServiceProvider.GetRequiredService<HarvestTopPlayersUseCase>();
                await harvest.ExecuteAsync(force: true, ct: CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual top harvest failed");
                // Сбор упал раньше, чем успел записать причину, - пишем её свежим
                // контекстом, иначе панель так и показывала бы прошлую попытку.
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<HarvestTopPlayersUseCase>()
                        .RecordFailureAsync($"сбор не запустился: {ex.GetType().Name}: {ex.Message}");
                }
                catch (Exception inner) { logger.LogError(inner, "Could not record harvest failure"); }
            }
            finally
            {
                Interlocked.Exchange(ref _harvestRunning, 0);
            }
        });

        return Accepted(new { started = true, running = true });
    }

    /// <summary>
    /// GET /api/owner/top/status — что со снимками мирового топа.
    ///
    /// Отдельно от сбора: узнать, почему снимка нет, должно быть можно НЕ запуская
    /// тысячу запросов к API игры. Раньше единственным способом было нажать «собрать»
    /// и ждать, а до появления этой ручки — вообще никак: причину знал воркер, и знал
    /// он её в своей памяти, в другом контейнере.
    /// </summary>
    [HttpGet("top/status")]
    public async Task<IActionResult> TopStatus(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Maintenance, ct) is { } deny) return deny;

        var last = await harvestTop.LastAsync(ct);
        var days = await topPlayers.DaysAsync(14, ct);
        return Ok(new
        {
            latestDay = days.FirstOrDefault(),
            daysStored = days.Count,
            lastAttemptAtUtc = last?.AtUtc,
            lastRows = last?.Rows ?? 0,
            lastProblem = last?.Problem,
            lastMeta = last?.Meta,
            running = Volatile.Read(ref _harvestRunning) == 1,
        });
    }

    /// <summary>
    /// POST /api/owner/sponsor — выдать спонсорство игроку. Body: { playerTag, days }.
    /// days = 0 снимает.
    ///
    /// Продление именно продлевает: если у человека ещё две недели, месяц сверху
    /// даёт полтора, а не обнуляет остаток. Иначе продлевать выгоднее было бы в
    /// последний день, и любой, кто заплатил заранее, терял бы своё.
    /// </summary>
    [HttpPost("sponsor")]
    public async Task<IActionResult> GrantSponsor([FromBody] GrantSponsorRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;

        var tag = LinkPlayerUseCase.Normalize(req.PlayerTag ?? "");
        if (tag.Length < 3) return BadRequest(new { error = "bad_tag" });
        if (req.Days is < 0 or > 366) return BadRequest(new { error = "bad_days" });

        // Отслеживаемая запись, а не строка из общего списка: тот читается без
        // отслеживания, и выданное спонсорство тихо терялось при сохранении —
        // ручка отвечала «ок» с новой датой, посчитанной в памяти, а в базе не
        // менялось ничего.
        var player = await players.GetByTagAsync(tag, ct);
        if (player is null) return NotFound(new { error = "player_not_found" });

        var now = DateTime.UtcNow;
        if (req.Days == 0)
        {
            player.SponsorUntilUtc = null;
            player.SponsorBackgroundKey = null;
            player.SponsorClanBackgroundKey = null;
        }
        else
        {
            var from = player.SponsorUntilUtc is { } until && until > now ? until : now;
            player.SponsorUntilUtc = from.AddDays(req.Days);
        }

        await players.SaveChangesAsync(ct);
        // Иначе выданный фон появится на Аллее только через пять минут кэша
        await HallCache.BumpAsync(settings, ct);
        return Ok(new { playerTag = player.PlayerTag, name = player.Name, until = player.SponsorUntilUtc });
    }

    public record SponsorSalesRequest(int Stars, int Days);
    public record CampaignRequest(string Code, string Name);

    /// <summary>
    /// GET /api/owner/campaigns — воронка по рекламным кампаниям и рефералам:
    /// пришли, привязали тег, подключили клан, заплатили.
    /// </summary>
    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.AppSettings, ct) is { } deny) return deny;
        return Ok(await campaignFunnel.ExecuteAsync(ct));
    }

    /// <summary>
    /// POST /api/owner/campaigns — завести кампанию. Body: { code, name }.
    /// Ссылка для неё — t.me/&lt;бот&gt;?start=ad_&lt;code&gt;, её собирает панель.
    /// </summary>
    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CampaignRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.AppSettings, ct) is { } deny) return deny;

        var code = (req.Code ?? "").Trim().ToLowerInvariant();
        var name = (req.Name ?? "").Trim();
        if (!Campaign.IsValidCode(code)) return BadRequest(new { error = "bad_code" });
        if (name.Length is < 1 or > 60) return BadRequest(new { error = "bad_name" });
        if (await campaigns.CodeExistsAsync(code, ct)) return Conflict(new { error = "code_taken" });

        await campaigns.AddAsync(new Campaign { Code = code, Name = name, CreatedAtUtc = DateTime.UtcNow }, ct);
        return Ok(new { code, name });
    }

    /// <summary>
    /// GET /api/owner/sponsor/sales — цена, срок и последние оплаты звёздами.
    ///
    /// Оплаты показываются здесь, а не только в балансе бота у BotFather: на
    /// вопрос «я заплатил, где спонсорство?» надо отвечать по журналу с номером
    /// платежа, а не по общей сумме на балансе.
    /// </summary>
    [HttpGet("sponsor/sales")]
    public async Task<IActionResult> GetSponsorSales(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;

        var offer = await SponsorSales.ReadAsync(settings, ct);
        var recent = await sponsorPayments.GetRecentAsync(30, ct);
        return Ok(new
        {
            stars = offer.Stars,
            days = offer.Days,
            // След последней оплаты: где именно она остановилась, если звёзды
            // списались, а спонсорства нет. Без него причину знал только лог воркера.
            lastCheckout = await PaymentTrace.ReadAsync(settings, PaymentTrace.CheckoutKey, ct),
            lastPaid = await PaymentTrace.ReadAsync(settings, PaymentTrace.PaidKey, ct),
            totalStars = await sponsorPayments.TotalStarsAsync(ct),
            payments = recent.Select(p => new
            {
                p.PlayerTag, p.Stars, p.Days, p.PaidAtUtc, p.TelegramChargeId, p.Kind, p.RefundedAtUtc,
            }),
        });
    }

    /// <summary>POST /api/owner/sponsor/sales — поставить цену в звёздах и срок. 0 звёзд — продажа выключена.</summary>
    [HttpPost("sponsor/sales")]
    public async Task<IActionResult> SetSponsorSales([FromBody] SponsorSalesRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;
        if (req.Stars is < 0 or > SponsorSales.MaxStars) return BadRequest(new { error = "bad_stars" });
        if (req.Days is < 1 or > SponsorSales.MaxDays) return BadRequest(new { error = "bad_days" });

        await settings.SetAsync(SponsorSales.PriceKey, req.Stars.ToString(), ct);
        await settings.SetAsync(SponsorSales.DaysKey, req.Days.ToString(), ct);
        return Ok(new { stars = req.Stars, days = req.Days });
    }

    /// <summary>
    /// GET /api/owner/plus — условия продажи Плюса и как он продаётся: сколько
    /// действующих, сколько триалов и сколько из них заплатили.
    /// </summary>
    [HttpGet("plus")]
    public async Task<IActionResult> GetPlus(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;

        var offer = await PlusSales.ReadAsync(settings, ct);
        var now = DateTime.UtcNow;
        var all = await entitlements.GetAllAsync(Entitlement.Skus.Plus, ct);
        var live = all.Where(e => e.RevokedAtUtc is null).ToList();

        // Купленное за звёзды: себе или в подарок. Плюс внутри спонсорства звёзд не несёт -
        // они учтены в платеже спонсорства.
        var purchases = live.Where(e => e.Stars > 0).ToList();
        var buyers = purchases.Select(e => e.GiverTelegramUserId ?? e.TelegramUserId).ToHashSet();
        var weekAgo = now.AddDays(-7);

        // «Стоп-тильт»: работает ли главный продукт. Цели плана - от 30% сигналов с паузой,
        // «🔕» не растёт, из получивших бесплатные сигналы покупает хотя бы каждый десятый.
        var alerts = (await tiltAlerts.GetAllSinceAsync(now.AddDays(-30), ct)).Where(a => a.Kind == "streak").ToList();
        var week = alerts.Where(a => a.SentUtc >= weekAgo).ToList();
        var freeUsers = alerts.Where(a => a.Free).Select(a => a.TelegramUserId).ToHashSet();
        var freeThenBought = freeUsers.Count(tg => purchases.Any(e => e.TelegramUserId == tg
            && e.CreatedAtUtc >= alerts.Where(a => a.TelegramUserId == tg && a.Free).Min(a => a.SentUtc)));

        // Трекер боёв: сколько включено, сколько выключили и нажали «🔕» за неделю, сколько карточек.
        var (trackerOn, trackerTotal) = await alertPrefs.TrackerCountsAsync(ct);
        var tracker = new
        {
            enabled = trackerOn,
            total = trackerTotal,
            offWeek = (await sentLog.GetKeysAsync(TrackerActionsUseCase.OffKind, weekAgo, ct)).Count,
            mutesWeek = (await sentLog.GetKeysAsync(TrackerActionsUseCase.MuteKind, weekAgo, ct)).Count,
            cardsWeek = (await sentLog.GetKeysAsync(BattleTrackerUseCase.SentKind, weekAgo, ct)).Count,
            dm = !string.Equals(await settings.GetAsync(BattleTrackerUseCase.KillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase),
            beta = await settings.GetAsync(BattleTrackerUseCase.BetaKey, ct) ?? "",
        };

        return Ok(new
        {
            paywall = offer.Paywall,
            price7 = offer.Price7,
            price30 = offer.Price30,
            active = live.Where(e => e.UntilUtc > now).Select(e => e.TelegramUserId).Distinct().Count(),
            buyers = buyers.Count,
            purchases = purchases.Count,
            stars = purchases.Sum(e => e.Stars),
            starsWeek = purchases.Where(e => e.CreatedAtUtc >= weekAgo).Sum(e => e.Stars),
            gifts = live.Count(e => e.Source == Entitlement.Sources.Gift),
            alertsWeek = week.Count,
            pausedWeek = week.Count(a => a.Choice == "pause"),
            mutedWeek = week.Count(a => a.Choice == "mute"),
            freeUsers = freeUsers.Count,
            freeThenBought,
            tracker,
            recent = all.OrderByDescending(e => e.CreatedAtUtc).Take(30).Select(e => new
            {
                e.TelegramUserId, e.PlayerTag, e.Source, e.Days, e.Stars, e.UntilUtc, e.CreatedAtUtc,
                revoked = e.RevokedAtUtc is not null,
            }),
        });
    }

    /// <summary>
    /// POST /api/owner/plus/settings — цены пропусков и выключатель платного.
    /// Выключатель - аварийный: всё платное становится бесплатным для всех, продажа закрывается.
    /// </summary>
    [HttpPost("plus/settings")]
    public async Task<IActionResult> SetPlus([FromBody] PlusSettingsRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;
        if (req.Price7 is < 1 or > PlusSales.MaxStars || req.Price30 is < 1 or > PlusSales.MaxStars)
            return BadRequest(new { error = "bad_stars" });
        await settings.SetAsync(PlusSales.PaywallKey, req.Paywall ? "on" : "off", ct);
        await settings.SetAsync(PlusSales.Price7Key, req.Price7.ToString(), ct);
        await settings.SetAsync(PlusSales.Price30Key, req.Price30.ToString(), ct);
        return Ok(new { paywall = req.Paywall, price7 = req.Price7, price30 = req.Price30 });
    }

    /// <summary>
    /// POST /api/owner/tracker/settings — рубильник карточек трекера и закрытый тест.
    /// Body: { dm, beta }: dm=false - новых карточек нет; beta - id Telegram через запятую, пусто - у всех.
    /// </summary>
    [HttpPost("tracker/settings")]
    public async Task<IActionResult> SetTracker([FromBody] TrackerSettingsRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;
        var beta = string.Join(",", (req.Beta ?? "")
            .Split(new[] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => long.TryParse(x, out _)));
        await settings.SetAsync(BattleTrackerUseCase.KillSwitchKey, req.Dm ? "on" : "off", ct);
        await settings.SetAsync(BattleTrackerUseCase.BetaKey, beta, ct);
        return Ok(new { dm = req.Dm, beta });
    }

    /// <summary>
    /// POST /api/owner/plus/grant — выдать Плюс руками (подарок, компенсация, блогеру).
    /// Body: { playerTag, days }. Выдаётся на Telegram-аккаунт, к которому привязан тег.
    /// </summary>
    [HttpPost("plus/grant")]
    public async Task<IActionResult> GrantPlus([FromBody] GrantPlusRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;
        if (req.Days is < 1 or > 366) return BadRequest(new { error = "bad_days" });

        var tag = LinkPlayerUseCase.Normalize(req.PlayerTag ?? "");
        var player = await players.GetByTagAsync(tag, ct);
        if (player is null) return NotFound(new { error = "player_not_found" });
        if (player.TelegramUserId is not long tg) return BadRequest(new { error = "player_not_linked" });

        var until = await plusAccess.GrantAsync(tg, req.Days, Entitlement.Sources.Grant, player.PlayerTag, 0, null, ct);
        return Ok(new { playerTag = player.PlayerTag, name = player.Name, until });
    }

    /// <summary>
    /// POST /api/owner/payments/refund — вернуть звёзды за платёж и отозвать купленное.
    /// Body: { chargeId }. Сначала возврат в Telegram, потом отметка: отметить возврат,
    /// которого Telegram не сделал, значило бы отобрать у человека оплаченное.
    /// </summary>
    [HttpPost("payments/refund")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.Sponsors, ct) is { } deny) return deny;

        var payment = await sponsorPayments.GetByChargeAsync(req.ChargeId ?? "", ct);
        if (payment is null) return NotFound(new { error = "payment_not_found" });
        if (payment.RefundedAtUtc is not null) return Conflict(new { error = "already_refunded" });

        try
        {
            await bot.RefundStarPayment(payment.PayerTelegramUserId, payment.TelegramChargeId, ct);
        }
        catch (ApiRequestException ex)
        {
            // Уже возвращено раньше (например, из самого Telegram) - отметим и у себя,
            // иначе журнал так и будет врать, что деньги у нас.
            if (ex.Message.Contains("CHARGE_ALREADY_REFUNDED", StringComparison.OrdinalIgnoreCase))
            {
                await revokePurchase.MarkRefundedAsync(payment.TelegramChargeId, ct);
                return Ok(new { refunded = true, note = "already_refunded_in_telegram" });
            }
            logger.LogWarning(ex, "Refund failed for {Charge}", payment.TelegramChargeId);
            return StatusCode(502, new { error = "refund_failed", message = ex.Message });
        }

        await revokePurchase.MarkRefundedAsync(payment.TelegramChargeId, ct);
        logger.LogInformation("Refunded {Stars} XTR, charge {Charge}", payment.Stars, payment.TelegramChargeId);
        return Ok(new { refunded = true });
    }

    /// <summary>GET /api/owner/sponsors — действующие спонсоры.</summary>
    [HttpGet("sponsors")]
    public async Task<IActionResult> GetSponsors(CancellationToken ct)
    {
        if (await DenyViewAsync(ct) is { } deny) return deny;

        var now = DateTime.UtcNow;
        var all = await players.GetAllLinkedAsync(ct);
        return Ok(all
            .Where(p => p.IsSponsor(now))
            .OrderBy(p => p.SponsorUntilUtc)
            .Select(p => new
            {
                p.PlayerTag,
                p.Name,
                clanName = p.Clan?.Name,
                until = p.SponsorUntilUtc,
                background = p.SponsorBackgroundKey,
                daysLeft = (int)Math.Ceiling((p.SponsorUntilUtc!.Value - now).TotalDays),
            }));
    }

    /// <summary>
    /// POST /api/owner/tabs — какие вкладки показывать внизу. Body: { tabs: ["clan", …] }.
    /// Порядок в списке — порядок на экране.
    /// </summary>
    [HttpPost("tabs")]
    public async Task<IActionResult> SetTabs([FromBody] TabsRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.AppSettings, ct) is { } deny) return deny;

        var tabs = (req.Tabs ?? [])
            .Select(x => x?.Trim().ToLowerInvariant() ?? "")
            .Where(AppTabs.IsKnown)
            .Distinct()
            .ToArray();

        // Пустой набор оставил бы приложение вообще без навигации, и починить это
        // было бы уже нечем — панель сама живёт на вкладке.
        if (tabs.Length == 0) return BadRequest(new { error = "no_tabs" });

        await settings.SetAsync(AppTabs.SettingKey, string.Join(',', tabs), ct);
        return Ok(new { tabs });
    }

    /// <summary>GET /api/owner/moderators — список модераторов (видит только владелец).</summary>
    [HttpGet("moderators")]
    public async Task<IActionResult> GetModerators(CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var list = await moderators.GetAllAsync(ct);
        return Ok(list.Select(m => new
        {
            m.Id,
            username = m.TelegramUsername,
            m.Note,
            permissions = PermissionNames(m.Permissions),
            m.AddedAtUtc,
            m.FirstSeenAtUtc,
            // Пока пусто — человек ещё ни разу не заходил, и запись держится
            // только на юзернейме. Фронт этим состоянием объясняет, почему так.
            confirmed = m.TelegramUserId is not null,
        }));
    }

    /// <summary>POST /api/owner/moderators — назначить по юзернейму. Body: { username, note? }.</summary>
    [HttpPost("moderators")]
    public async Task<IActionResult> AddModerator([FromBody] AddModeratorRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var username = ServiceModerator.NormalizeUsername(req.Username ?? "");
        // Правила Telegram: 5–32 знака, латиница, цифры и подчёркивание.
        if (username.Length is < 5 or > 32 || !username.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            return BadRequest(new { error = "bad_username", message = "Юзернейм: 5–32 знака, латиница, цифры, _" });

        var existing = await moderators.GetAllAsync(ct);
        if (existing.Any(m => m.TelegramUsername == username))
            return Conflict(new { error = "already_moderator" });

        var granted = await GrantableAsync(req.Permissions, ct);
        if (granted is null) return StatusCode(403, new { error = "cannot_grant" });

        var userId = (long)HttpContext.Items["TelegramUserId"]!;
        await moderators.AddAsync(new ServiceModerator
        {
            TelegramUsername = username,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            Permissions = granted.Value,
            AddedAtUtc = DateTime.UtcNow,
            AddedByTelegramUserId = userId,
        }, ct);
        await moderators.SaveChangesAsync(ct);

        return Ok(new { ok = true, username });
    }

    /// <summary>DELETE /api/owner/moderators/{id} — снять модератора.</summary>
    [HttpDelete("moderators/{id:int}")]
    public async Task<IActionResult> RemoveModerator(int id, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var target = await moderators.GetByIdAsync(id, ct);
        if (target is null) return NotFound(new { error = "moderator_not_found" });

        await moderators.RemoveAsync(target, ct);
        await moderators.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    /// <summary>PUT /api/owner/moderators/{id}/permissions — поменять набор прав.</summary>
    [HttpPut("moderators/{id:int}/permissions")]
    public async Task<IActionResult> SetPermissions(
        int id, [FromBody] SetPermissionsRequest req, CancellationToken ct)
    {
        if (await DenyAsync(ServicePermission.ManageModerators, ct) is { } deny) return deny;

        var target = await moderators.GetByIdAsync(id, ct);
        if (target is null) return NotFound(new { error = "moderator_not_found" });

        var granted = await GrantableAsync(req.Permissions, ct);
        if (granted is null) return StatusCode(403, new { error = "cannot_grant" });

        target.Permissions = granted.Value;
        await moderators.SaveChangesAsync(ct);
        return Ok(new { ok = true, permissions = PermissionNames(target.Permissions) });
    }

    /// <summary>
    /// Запрошенный набор прав, если выдающий вправе его выдать. null — не вправе.
    ///
    /// Выдать больше, чем есть у тебя самого, нельзя. Без этого правила модератор,
    /// которому доверили назначать других, выписал бы себе рассылку и тарифы за два
    /// нажатия — право назначать превратилось бы в право на всё сразу.
    /// Владельца это не касается: у него есть всё, и проверка для него всегда верна.
    /// </summary>
    private async Task<ServicePermission?> GrantableAsync(string[]? names, CancellationToken ct)
    {
        var wanted = ParsePermissions(names);
        var me = await CurrentAsync(ct);
        return me.Can(wanted) ? wanted : null;
    }

    /// <summary>
    /// Имена прав в набор флагов.
    ///
    /// Неизвестное имя молча пропускаем, а не падаем: фронт может оказаться старее
    /// сервера после выката, и в этом случае лучше сохранить то, что он прислал,
    /// чем отказать целиком и оставить человека вообще без прав.
    /// </summary>
    private static ServicePermission ParsePermissions(string[]? names)
    {
        var result = ServicePermission.None;
        foreach (var name in names ?? [])
            if (Enum.TryParse<ServicePermission>(name, ignoreCase: true, out var one)
                && one != ServicePermission.All && one != ServicePermission.None)
                result |= one;
        return result;
    }

    /// <summary>Набор флагов обратно в имена — по одному на каждый выставленный.</summary>
    private static string[] PermissionNames(ServicePermission permissions) =>
        Enum.GetValues<ServicePermission>()
            .Where(p => p != ServicePermission.None && p != ServicePermission.All)
            .Where(p => (permissions & p) == p)
            .Select(p => p.ToString())
            .ToArray();

    private Task<ServiceIdentity> CurrentAsync(CancellationToken ct) =>
        access.ResolveAsync(
            (long)HttpContext.Items["TelegramUserId"]!,
            HttpContext.Items["TelegramUsername"] as string,
            ct);

    /// <summary>Отказ, если панель недоступна вовсе. null — можно.</summary>
    private async Task<IActionResult?> DenyViewAsync(CancellationToken ct) =>
        (await CurrentAsync(ct)).HasPanel ? null : StatusCode(403, new { error = "not_owner" });

    /// <summary>
    /// Отказ, если нет конкретного права. null — можно.
    ///
    /// Код ошибки отличается от «панели нет вообще»: человек в панель попал, и
    /// сказать ему нужно не «тебя тут нет», а «этого тебе не выдали».
    /// </summary>
    private async Task<IActionResult?> DenyAsync(ServicePermission permission, CancellationToken ct)
    {
        var me = await CurrentAsync(ct);
        if (!me.HasPanel) return StatusCode(403, new { error = "not_owner" });
        return me.Can(permission)
            ? null
            : StatusCode(403, new { error = "no_permission", message = permission.ToString() });
    }
}
