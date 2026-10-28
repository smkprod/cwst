using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Оплата гривнами через банку Монобанка.
///
/// Человек берёт заказ - сумму и шестизначный код - и переводит деньги в банку, указав
/// код в комментарии. Воркер раз в минуту читает выписку банки (личный API владельца),
/// находит код и выдаёт доступ так же, как за звёзды: Плюс на аккаунт, Клан Про на строку
/// игрока вместе с Плюсом. Перевод без кода или с недоплатой ждёт владельца в панели.
/// </summary>
public class JarPaymentUseCase(
    IJarRepository jars,
    IServiceSettingRepository settings,
    IMonobankClient mono,
    PlusAccess plus,
    IPlayerRepository players,
    ISponsorPaymentRepository payments,
    IPlayerAlertPrefsRepository alertPrefs,
    INotificationSender sender,
    IFunnelRepository funnel)
{
    /// <summary>Монобанк пускает к выписке раз в минуту: такт воркера - чуть реже.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(65);

    /// <summary>Сколько выписки читать за раз: переживает простой воркера в пару суток.</summary>
    public static readonly TimeSpan PollWindow = TimeSpan.FromDays(3);

    public record Order(string Code, string Product, int Days, int AmountUah, string Link, bool Paid);

    /// <summary>Заказ на оплату. null с причиной - выдать нельзя (выключено, нет игрока, нет цены).</summary>
    public async Task<(Order? Order, string? Error)> CreateOrderAsync(long telegramUserId, string product, int days, CancellationToken ct = default)
    {
        var offer = await JarSales.ReadAsync(settings, ct);
        if (!offer.On) return (null, "jar_off");
        if (product is not (JarOrder.Products.Plus or JarOrder.Products.Pro)) return (null, "bad_product");
        if (product == JarOrder.Products.Pro) days = (await SponsorSales.ReadAsync(settings, ct)).Days;
        var price = offer.PriceFor(product, days);
        if (price <= 0) return (null, "bad_days");

        var player = await players.GetByTelegramIdAsync(telegramUserId, ct);
        if (product == JarOrder.Products.Pro && player is null) return (null, "player_not_linked");

        var now = DateTime.UtcNow;
        // Тот же незакрытый заказ - тот же код: человек мог уже перевести по старому
        var open = await jars.GetOpenOrderAsync(telegramUserId, product, days, now.AddDays(-3), ct);
        if (open is not null)
        {
            if (open.AmountUah != price) { open.AmountUah = price; await jars.SaveChangesAsync(ct); }
            return (ToDto(open, offer.Link!), null);
        }

        var code = JarSales.NewCode();
        for (var i = 0; i < 20 && await jars.CodeTakenAsync(code, now - JarSales.OrderLife - JarSales.OrderLife, ct); i++)
            code = JarSales.NewCode();

        var order = new JarOrder
        {
            Code = code,
            TelegramUserId = telegramUserId,
            PlayerId = player?.Id,
            Product = product,
            Days = days,
            AmountUah = price,
            CreatedUtc = now,
        };
        await jars.AddOrderAsync(order, ct);
        await PurchaseFunnel.MarkAsync(funnel, telegramUserId,
            product == JarOrder.Products.Pro ? FunnelEvent.Products.Sponsor : FunnelEvent.Products.Plus,
            FunnelEvent.Steps.Buy, "jar", ct);
        return (ToDto(order, offer.Link!), null);
    }

    /// <summary>Свой заказ по коду - для «Я оплатил, проверить». Чужой - как не найденный.</summary>
    public async Task<Order?> GetOrderAsync(long telegramUserId, string code, CancellationToken ct = default)
    {
        var order = await jars.GetByCodeAsync(code.Trim(), ct);
        if (order is null || order.TelegramUserId != telegramUserId) return null;
        var offer = await JarSales.ReadAsync(settings, ct);
        return ToDto(order, offer.Link ?? "");
    }

    private static Order ToDto(JarOrder o, string link) =>
        new(o.Code, o.Product, o.Days, o.AmountUah, link, o.PaidUtc is not null);

    public record TickResult(int NewTx, int Paid, string? Error);

    /// <summary>
    /// Прочитать выписку банки и выдать оплаченное. Без ключа или при выключенной
    /// оплате - ничего. Зовётся воркером раз в <see cref="PollEvery"/>.
    /// </summary>
    public async Task<TickResult> TickAsync(CancellationToken ct = default)
    {
        if (!mono.Configured) return new TickResult(0, 0, null);
        var offer = await JarSales.ReadAsync(settings, ct);
        if (!offer.On) return new TickResult(0, 0, null);
        var now = DateTime.UtcNow;

        try
        {
            var (account, fresh) = await AccountAsync(offer.Link!, ct);
            if (account is null)
            {
                await settings.SetAsync(JarSales.LastPollKey, $"{now:O}|jar_not_found", ct);
                return new TickResult(0, 0, "jar_not_found");
            }
            // Список банок и выписка - два запроса, а Монобанк пускает раз в минуту:
            // банку только что нашли - выписку читаем следующим тактом
            if (fresh)
            {
                await settings.SetAsync(JarSales.LastPollKey, $"{now:O}|ok", ct);
                return new TickResult(0, 0, null);
            }

            var txs = (await mono.StatementAsync(account, now - PollWindow, now, ct))
                .Where(t => t.AmountKop > 0)
                .ToList();
            var known = await jars.KnownTxAsync(txs.Select(t => t.Id).ToList(), ct);
            int added = 0, paid = 0;
            foreach (var tx in txs.Where(t => !known.Contains(t.Id)).OrderBy(t => t.TimeUtc))
            {
                var incoming = new JarIncoming
                {
                    MonoTxId = tx.Id,
                    AmountKop = tx.AmountKop,
                    Comment = Clip(tx.Comment, 500),
                    Sender = Clip(tx.Description, 200),
                    TimeUtc = tx.TimeUtc,
                    CreatedUtc = now,
                };
                await jars.AddIncomingAsync(incoming, ct);
                added++;

                var order = await MatchAsync(incoming, ct);
                if (order is not null && await ApplyAsync(order, incoming, manual: false, ct)) paid++;
                else await jars.SaveChangesAsync(ct);
            }

            await settings.SetAsync(JarSales.LastPollKey, $"{now:O}|ok", ct);
            return new TickResult(added, paid, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await settings.SetAsync(JarSales.LastPollKey, $"{now:O}|{Clip(ex.Message, 200)}", ct);
            return new TickResult(0, 0, ex.Message);
        }
    }

    private static string? Clip(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    /// <summary>id банки в API по ссылке из панели. Найденный запоминаем: список банок - тот же лимит раз в минуту.</summary>
    /// <param name="Fresh">Банку нашли только что - запрос в этот такт уже потрачен.</param>
    private async Task<(string? Id, bool Fresh)> AccountAsync(string link, CancellationToken ct)
    {
        if (await settings.GetAsync(JarSales.AccountForKey, ct) == link
            && await settings.GetAsync(JarSales.AccountKey, ct) is { Length: > 0 } cached)
            return (cached, false);

        var sendId = JarSales.SendIdOf(link);
        if (sendId is null) return (null, false);
        var jar = (await mono.JarsAsync(ct)).FirstOrDefault(j => JarSales.SameJar(j.SendId, sendId));
        if (jar is null) return (null, false);
        await settings.SetAsync(JarSales.AccountKey, jar.Id, ct);
        await settings.SetAsync(JarSales.AccountForKey, link, ct);
        return (jar.Id, true);
    }

    /// <summary>Заказ по коду из комментария: свежий, неоплаченный, и сумма не меньше цены.</summary>
    private async Task<JarOrder?> MatchAsync(JarIncoming incoming, CancellationToken ct)
    {
        foreach (var code in JarSales.CodesIn(incoming.Comment))
        {
            var order = await jars.GetByCodeAsync(code, ct);
            if (order is null || order.PaidUtc is not null) continue;
            if (incoming.TimeUtc < order.CreatedUtc.AddMinutes(-5) || incoming.TimeUtc > order.CreatedUtc + JarSales.OrderLife) continue;
            if (incoming.AmountKop < order.AmountUah * 100L) continue;
            return order;
        }
        return null;
    }

    /// <summary>
    /// Владелец подтверждает оплату руками: по коду заказа и, если есть, переводу из
    /// выписки. Перевод с недоплатой тоже можно засчитать - это решение владельца.
    /// </summary>
    public async Task<(bool Ok, string? Error)> ConfirmAsync(string code, int? incomingId, CancellationToken ct = default)
    {
        var order = await jars.GetByCodeAsync(code.Trim(), ct);
        if (order is null) return (false, "order_not_found");
        if (order.PaidUtc is not null) return (false, "already_paid");
        JarIncoming? incoming = null;
        if (incomingId is int id)
        {
            incoming = await jars.GetIncomingAsync(id, ct);
            if (incoming is null) return (false, "payment_not_found");
            if (incoming.OrderId is not null) return (false, "payment_used");
        }
        return await ApplyAsync(order, incoming, manual: true, ct) ? (true, null) : (false, "player_not_found");
    }

    /// <summary>Выдать оплаченное: доступ, запись в журнал платежей и сообщение человеку.</summary>
    private async Task<bool> ApplyAsync(JarOrder order, JarIncoming? incoming, bool manual, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var charge = $"jar:{order.Id}";
        var player = order.PlayerId is int pid
            ? await players.GetByIdAsync(pid, ct)
            : await players.GetByTelegramIdAsync(order.TelegramUserId, ct);

        DateTime until;
        string kind;
        if (order.Product == JarOrder.Products.Pro)
        {
            if (player is null) return false;
            // Продление, а не перезапись - как за звёзды
            var from = player.SponsorUntilUtc is { } u && u > now ? u : now;
            player.SponsorUntilUtc = from.AddDays(order.Days);
            until = player.SponsorUntilUtc.Value;
            await plus.GrantAsync(order.TelegramUserId, order.Days, Entitlement.Sources.Sponsor, player.PlayerTag, 0, charge,
                ct, save: false);
            kind = SponsorPayment.Kinds.Sponsor;
        }
        else
        {
            until = await plus.GrantAsync(order.TelegramUserId, order.Days, Entitlement.Sources.Jar, player?.PlayerTag, 0, charge,
                ct, save: false);
            kind = SponsorPayment.Kinds.Plus;
        }

        order.PaidUtc = now;
        order.Manual = manual;
        order.PaidKop = incoming?.AmountKop;
        if (incoming is not null) incoming.OrderId = order.Id;

        // Тот же журнал, что у звёзд: по нему воронка считает «заплатили». Звёзд - ноль,
        // гривны - в заказе.
        await payments.AddAsync(new SponsorPayment
        {
            PayerTelegramUserId = order.TelegramUserId,
            PlayerTag = player?.PlayerTag ?? "",
            Stars = 0,
            Days = order.Days,
            TelegramChargeId = charge,
            PaidAtUtc = now,
            Kind = kind,
        }, ct);

        // Один контекст: заказ, перевод, доступ и платёж сохраняются вместе
        await jars.SaveChangesAsync(ct);
        if (order.Product == JarOrder.Products.Pro) await HallCache.BumpAsync(settings, ct);

        var prefs = await alertPrefs.GetAsync(order.TelegramUserId, ct);
        var local = until.AddMinutes(prefs?.TzOffsetMinutes ?? Battles.TiltMessages.DefaultTzOffsetMinutes);
        try { await sender.SendDmAsync(order.TelegramUserId, PaidText(order.Product, local, prefs?.Lang), [], ct: ct); }
        catch { /* доступ уже выдан - сообщение не главное */ }
        return true;
    }

    /// <param name="untilLocal">Конец срока по местному времени человека.</param>
    public static string PaidText(string product, DateTime untilLocal, string? lang)
    {
        var date = untilLocal.ToString("dd.MM.yyyy");
        return (lang, product) switch
        {
            ("uk", JarOrder.Products.Pro) => $"✅ Оплату отримано — Клан Про діє до {date}. Дякуємо, що підтримуєш Clanify!",
            ("uk", _) => $"✅ Оплату отримано — Плюс діє до {date}. Дякуємо, що підтримуєш Clanify!",
            ("en", JarOrder.Products.Pro) => $"✅ Payment received — Clan Pro is active until {date}. Thanks for supporting Clanify!",
            ("en", _) => $"✅ Payment received — Plus is active until {date}. Thanks for supporting Clanify!",
            (_, JarOrder.Products.Pro) => $"✅ Оплата получена — Клан Про действует до {date}. Спасибо, что поддерживаешь Clanify!",
            _ => $"✅ Оплата получена — Плюс действует до {date}. Спасибо, что поддерживаешь Clanify!",
        };
    }

    public record OwnerOrder(int Id, string Code, long TelegramUserId, string? Name, string? Tag, string Product, int Days,
        int AmountUah, DateTime CreatedUtc, DateTime? PaidUtc, long? PaidKop, bool Manual);

    public record OwnerIncoming(int Id, long AmountKop, string? Comment, string? Sender, DateTime TimeUtc);

    public record OwnerView(bool On, string? Link, int Plus7, int Plus30, int Pro, bool TokenSet, bool JarFound,
        DateTime? LastPollUtc, string? LastPollStatus, List<OwnerOrder> Orders, List<OwnerIncoming> Unmatched,
        long PaidKop30);

    public async Task<OwnerView> OwnerViewAsync(CancellationToken ct = default)
    {
        var offer = await JarSales.ReadAsync(settings, ct);
        var rawLink = JarSales.NormalizeLink(await settings.GetAsync(JarSales.LinkKey, ct));
        var on = string.Equals(await settings.GetAsync(JarSales.OnKey, ct), "on", StringComparison.OrdinalIgnoreCase);
        var since = DateTime.UtcNow.AddDays(-30);
        var orders = await jars.GetOrdersSinceAsync(since, ct);

        var names = new Dictionary<long, (string Name, string Tag)>();
        foreach (var tg in orders.Select(o => o.TelegramUserId).Distinct())
            if (await players.GetByTelegramIdAsync(tg, ct) is { } p) names[tg] = (p.Name, p.PlayerTag);

        var poll = await settings.GetAsync(JarSales.LastPollKey, ct);
        DateTime? pollAt = null;
        string? pollStatus = null;
        if (poll?.Split('|', 2) is [var at, var status] && DateTime.TryParse(at, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
        {
            pollAt = t;
            pollStatus = status;
        }

        return new OwnerView(
            on, rawLink, offer.Plus7, offer.Plus30, offer.Pro,
            mono.Configured,
            rawLink is not null && await settings.GetAsync(JarSales.AccountForKey, ct) == rawLink,
            pollAt, pollStatus,
            orders.Select(o => new OwnerOrder(o.Id, o.Code, o.TelegramUserId,
                names.TryGetValue(o.TelegramUserId, out var n) ? n.Name : null,
                names.TryGetValue(o.TelegramUserId, out var n2) ? n2.Tag : null,
                o.Product, o.Days, o.AmountUah, o.CreatedUtc, o.PaidUtc, o.PaidKop, o.Manual)).ToList(),
            (await jars.GetUnmatchedSinceAsync(since, ct))
                .Select(i => new OwnerIncoming(i.Id, i.AmountKop, i.Comment, i.Sender, i.TimeUtc)).ToList(),
            orders.Where(o => o.PaidUtc is not null).Sum(o => o.PaidKop ?? o.AmountUah * 100L));
    }

    /// <summary>Настройки из панели. Ссылка - только на банку Монобанка.</summary>
    public async Task<string?> SaveSettingsAsync(bool on, string? link, int plus7, int plus30, int pro, CancellationToken ct = default)
    {
        var clean = JarSales.NormalizeLink(link);
        if (!string.IsNullOrWhiteSpace(link) && clean is null) return "bad_link";
        if (on && clean is null) return "no_link";
        if (plus7 is < 1 or > JarSales.MaxUah || plus30 is < 1 or > JarSales.MaxUah || pro is < 1 or > JarSales.MaxUah)
            return "bad_price";
        await settings.SetAsync(JarSales.OnKey, on ? "on" : "off", ct);
        await settings.SetAsync(JarSales.LinkKey, clean ?? "", ct);
        await settings.SetAsync(JarSales.Plus7Key, plus7.ToString(), ct);
        await settings.SetAsync(JarSales.Plus30Key, plus30.ToString(), ct);
        await settings.SetAsync(JarSales.ProKey, pro.ToString(), ct);
        return null;
    }
}
