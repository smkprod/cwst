using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Трекер боёв: одна тихая карточка захода, которая правится после каждого боя, и
/// итог, когда заход кончился.
///
/// Правила, которые не нарушаем никогда:
/// - трекер не пишет со звуком: со звуком пишет только «Стоп-тильт»;
/// - новое сообщение - только первый бой захода, дальше правка (правка не пищит);
/// - отметка «бой учтён» пишется до отправки: лучше пропустить карточку, чем прислать две;
/// - бой старше 20 минут карточку не создаёт: синхронизация после простоя никому не пишет.
///
/// Работает внутри опроса «Стоп-тильта» (тот же журнал, тот же ритм): отдельных
/// запросов к API игры у трекера нет.
/// </summary>
public class BattleTrackerUseCase(
    IPlayerBattleRepository battles,
    IPlayerAlertPrefsRepository alertPrefs,
    ITiltAlertRepository alerts,
    ISentNotificationRepository sentLog,
    IServiceSettingRepository settings,
    IClashRoyaleApi crApi,
    INotificationSender sender)
{
    /// <summary>Рубильник владельца: «off» - новых карточек и итогов нет, правки остаются.</summary>
    public const string KillSwitchKey = "tracker.dm";

    /// <summary>Закрытый тест: id Telegram через запятую. Пусто - трекер у всех.</summary>
    public const string BetaKey = "tracker.beta";

    /// <summary>Вид отметки в журнале отправленного - по нему считаем карточки за день.</summary>
    public const string SentKind = "trk";

    public const int MaxCardsPerDay = 6;

    /// <summary>Бой старше этого - уже история, а не «только что сыграл».</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(20);

    private IClashRoyaleApi CrApi => crApi;
    private IPlayerBattleRepository Battles => battles;
    private IServiceSettingRepository Settings => settings;

    public static bool Tracking(PlayerAlertPrefs? p) =>
        p is not null && (p.TrackerEnabled || p.TrackerCardMessageId is not null);

    /// <summary>Список закрытого теста. null - теста нет, трекер у всех.</summary>
    public static async Task<HashSet<long>?> BetaAsync(IServiceSettingRepository settings, CancellationToken ct)
    {
        var raw = await settings.GetAsync(BetaKey, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var ids = raw.Split(new[] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => long.TryParse(x.Trim(), out var v) ? v : 0)
            .Where(v => v != 0)
            .ToHashSet();
        return ids.Count == 0 ? null : ids;
    }

    public static bool Allowed(HashSet<long>? beta, long tg) => beta is null || beta.Contains(tg);

    /// <param name="Recent">Бои за последние 6 часов (все режимы), по возрастанию времени, уже после чтения журнала.</param>
    /// <param name="Threshold">Порог серии «Стоп-тильта»: в серии совет «смени колоду» не даём.</param>
    public record Ctx(
        long Tg, Player Player, PlayerAlertPrefs Prefs, bool Paid, IReadOnlyList<PlayerBattle> Recent,
        BotText T, int Tz, DateTime Now, int Threshold);

    /// <param name="AlertPrefix">Строка о последнем бое - для тильт-сигнала, если он уйдёт в этот же проход.</param>
    public record Tick(string? AlertPrefix, bool Created, bool Edited, bool Summarized);

    public async Task<Tick> StepAsync(Ctx c, CancellationToken ct = default)
    {
        var s = new State(this, c, ct);
        var p = c.Prefs;
        string? prefix = null;
        bool created = false, edited = false, summarized = false;

        if (p.TrackerEnabled)
        {
            if (p.TrackerWatermarkUtc is null)
            {
                // Включили, а отметки нет: всё, что уже сыграно, - история, не карточка.
                var newestStored = c.Recent.Count > 0 ? c.Recent[^1].BattleTimeUtc : c.Now;
                p.TrackerWatermarkUtc = newestStored > c.Now ? newestStored : c.Now;
                await alertPrefs.SaveChangesAsync(ct);
            }

            var watermark = p.TrackerWatermarkUtc.Value;
            var fresh = c.Recent.Where(b => b.BattleTimeUtc > watermark).ToList();
            if (fresh.Count > 0)
            {
                var newest = fresh[^1];
                // Отметку - до любой отправки: упадём посреди - бой не придёт второй раз.
                p.TrackerWatermarkUtc = newest.BattleTimeUtc;
                await alertPrefs.SaveChangesAsync(ct);

                var session = BattleAnalyzer.SessionAround(c.Recent, newest.BattleTimeUtc);
                var start = session[0].BattleTimeUtc;

                // Начался новый заход, а карточка прошлого ещё висит - сначала итог прошлого.
                if (p.TrackerCardMessageId is not null && p.TrackerCardStartUtc != start)
                    summarized |= await CloseAsync(s);

                var sameCard = p.TrackerCardMessageId is not null && p.TrackerCardStartUtc == start;
                var recent = c.Now - newest.BattleTimeUtc <= MaxAge;
                if (sameCard || recent)
                {
                    var catalog = await s.CatalogAsync();
                    var month = await s.MonthAsync();
                    var report = MatchReport.Build(newest, MatchDetailCodec.Decode(newest.DetailJson), month, true, catalog);
                    prefix = TrackerText.AlertPrefix(c.T, report);

                    var plusLine = PlusLine(c, newest, session, report, catalog);
                    var text = TrackerText.Card(c.T, report, session, c.Tz, catalog, plusLine,
                        p.PauseUntilUtc > c.Now ? p.PauseUntilUtc : null);
                    var buttons = TrackerText.CardButtons(c.T, newest.Id);

                    // Под карточкой уже громкий тильт-сигнал - переносим карточку под него,
                    // иначе свежий счёт окажется выше сигнала и его не увидят.
                    if (sameCard && !await AlertAfterCardAsync(c, ct))
                    {
                        edited = await sender.EditUserMessageAsync(c.Tg, p.TrackerCardMessageId!.Value, text, buttons, ct);
                        if (!edited)
                        {
                            // Карточку удалили - считаем, что её нет.
                            p.TrackerCardMessageId = null;
                            p.TrackerCardStartUtc = null;
                        }
                    }
                    if (!edited && recent) created = await CreateAsync(s, text, buttons, start);
                    await alertPrefs.SaveChangesAsync(ct);
                }
            }
        }

        // Заход кончился (30 минут без боёв) - итог. Проверяем каждый проход, а не только после боя.
        if (!summarized && p.TrackerCardMessageId is not null && p.TrackerCardStartUtc is DateTime cardStart)
        {
            var last = SessionOf(c.Recent, cardStart) is { Count: > 0 } open ? open[^1].BattleTimeUtc : (DateTime?)null;
            var over = last is DateTime l
                ? c.Now - l >= TiltWatchUseCase.SessionEnd
                : c.Now - cardStart > TimeSpan.FromHours(6);
            if (over) summarized = await CloseAsync(s);
        }

        return new Tick(prefix, created, edited, summarized);
    }

    /// <summary>
    /// Строка Плюса в карточке. С Плюсом - всегда, когда есть что сказать. Без него -
    /// раз в день, на первом поражении, где она нашлась, с пометкой 🎁: попробовать, а
    /// не прочитать про замок.
    /// </summary>
    private static string? PlusLine(
        Ctx c, PlayerBattle newest, List<PlayerBattle> session, MatchReport.Report report,
        IReadOnlyDictionary<int, CrCatalogCard> catalog)
    {
        var streak = 0;
        for (var i = session.Count - 1; i >= 0 && session[i].Result < 0; i--) streak++;
        var line = TrackerText.PlusLine(c.T, report, streak >= c.Threshold, catalog);
        if (line is null) return null;
        if (c.Paid) return "📈 " + line;

        var p = c.Prefs;
        var midnight = TiltMessages.LocalMidnightUtc(c.Now, c.Tz);
        if (newest.Result >= 0 || p.TrackerHintBattleUtc >= midnight) return null;
        p.TrackerHintBattleUtc = newest.BattleTimeUtc;
        return "🎁 " + line;
    }

    /// <summary>После карточки пришёл тильт-сигнал (он ниже карточки в чате).</summary>
    private async Task<bool> AlertAfterCardAsync(Ctx c, CancellationToken ct)
    {
        if (c.Prefs.TrackerCardMessageId is not int card || c.Prefs.TrackerCardStartUtc is not DateTime start) return false;
        var since = await alerts.GetSinceAsync(c.Tg, start.AddMinutes(-1), ct);
        return since.Any(a => a.MessageId > card);
    }

    private async Task<bool> CreateAsync(State s, string text, IReadOnlyList<IReadOnlyList<BotButton>> buttons, DateTime start)
    {
        var c = s.Ctx;
        var p = c.Prefs;
        if (p.TrackerMutedUntilUtc > c.Now || p.MutedUntilUtc > c.Now) return false;
        if (await s.KilledAsync()) return false;

        var today = await sentLog.GetKeysAsync(SentKind, TiltMessages.LocalMidnightUtc(c.Now, c.Tz), s.Ct);
        if (today.Count(k => k.StartsWith($"{c.Tg}|", StringComparison.Ordinal)) >= MaxCardsPerDay) return false;

        // Переезд под тильт-сигнал: старую карточку убираем, а не оставляем двойником.
        if (p.TrackerCardMessageId is int old)
        {
            if (!await sender.DeleteUserMessageAsync(c.Tg, old, s.Ct))
                await sender.EditUserMessageAsync(c.Tg, old, c.T.TrkBelow, null, s.Ct);
            p.TrackerCardMessageId = null;
            p.TrackerCardStartUtc = null;
        }

        var sent = await sender.SendDmAsync(c.Tg, text, buttons, silent: true, s.Ct);
        if (sent.MessageId is int id)
        {
            p.TrackerCardMessageId = id;
            p.TrackerCardStartUtc = start;
            await sentLog.AddAsync(SentKind, $"{c.Tg}|{start:O}|{id}", s.Ct);
            return true;
        }
        // Заблокировал - замолкаем до его сообщения боту. Временный сбой - следующий бой попробует снова.
        if (sent.Blocked) p.DmBlocked = true;
        return false;
    }

    /// <summary>
    /// Итог захода. Боёв два и больше - итог новым тихим сообщением, карточка
    /// удаляется. Бой один - карточка сама становится итогом.
    /// </summary>
    private async Task<bool> CloseAsync(State s)
    {
        var c = s.Ctx;
        var p = c.Prefs;
        if (p.TrackerCardMessageId is not int cardId || p.TrackerCardStartUtc is not DateTime start) return false;

        var session = SessionOf(c.Recent, start);
        p.TrackerCardMessageId = null;
        p.TrackerCardStartUtc = null;
        // Сначала забываем карточку, потом пишем: сбой посередине не пришлёт итог дважды.
        await alertPrefs.SaveChangesAsync(s.Ct);
        if (session.Count == 0) return false;

        var catalog = await s.CatalogAsync();
        var month = await s.MonthAsync();
        var locked = c.Paid ? 0 : session.Sum(b =>
            MatchReport.Build(b, MatchDetailCodec.Decode(b.DetailJson), month, false, catalog).LockedCount);
        var moment = c.Paid ? null : TrackerText.Moment(session, c.Threshold);
        var summary = TrackerText.Summary(c.T, session, c.Tz, c.Paid, catalog, locked, moment);
        var buttons = TrackerText.SummaryButtons(c.T, summary.Upsell);

        if (session.Count == 1 || await s.KilledAsync())
        {
            await sender.EditUserMessageAsync(c.Tg, cardId, summary.Text, buttons, s.Ct);
            return true;
        }

        var sent = await sender.SendDmAsync(c.Tg, summary.Text, buttons, silent: true, s.Ct);
        if (sent.Delivered)
        {
            if (!await sender.DeleteUserMessageAsync(c.Tg, cardId, s.Ct))
            {
                // Удалить не вышло - хотя бы убираем кнопки, чтобы не было двух «живых» сообщений.
                var last = session[^1];
                var report = MatchReport.Build(last, MatchDetailCodec.Decode(last.DetailJson), month, c.Paid, catalog);
                await sender.EditUserMessageAsync(c.Tg, cardId,
                    TrackerText.Card(c.T, report, session, c.Tz, catalog, null, null), null, s.Ct);
            }
        }
        else if (sent.Blocked) p.DmBlocked = true;
        else await sender.EditUserMessageAsync(c.Tg, cardId, summary.Text, buttons, s.Ct);
        return true;
    }

    /// <summary>Заход, который начался в <paramref name="start"/>. Пусто - его бои уже вне окна.</summary>
    private static List<PlayerBattle> SessionOf(IReadOnlyList<PlayerBattle> recent, DateTime start)
    {
        var first = recent.FirstOrDefault(b => b.BattleTimeUtc >= start);
        return first is null ? [] : BattleAnalyzer.SessionAround(recent, first.BattleTimeUtc);
    }

    /// <summary>Лениво загружаемое на один проход: справочник карт, месяц боёв, рубильник.</summary>
    private sealed class State(BattleTrackerUseCase owner, Ctx ctx, CancellationToken ct)
    {
        public Ctx Ctx => ctx;
        public CancellationToken Ct => ct;
        private Dictionary<int, CrCatalogCard>? _catalog;
        private List<PlayerBattle>? _month;
        private bool? _killed;

        public async Task<Dictionary<int, CrCatalogCard>> CatalogAsync() =>
            _catalog ??= await GetMetaDecksUseCase.SafeCatalogAsync(owner.CrApi, ct);

        public async Task<List<PlayerBattle>> MonthAsync() =>
            _month ??= await owner.Battles.GetSinceAsync(ctx.Player.PlayerTag, ctx.Now - CollectPlayerBattlesUseCase.Keep, ct);

        public async Task<bool> KilledAsync() =>
            _killed ??= string.Equals(await owner.Settings.GetAsync(KillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase);
    }
}
