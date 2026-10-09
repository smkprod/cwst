using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Meta;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Утренний дайджест в личку: как прошло вчера - счёт, лучшая серия, почему
/// проигрывал и против кого. Повод открыть приложение и сыграть ещё до вечера.
///
/// Пишем только тем, кто вчера реально играл (от трёх боёв в ладдере или на Пути
/// легенд): «вчера ты не играл» никому не нужно. Один раз в день, в 10 утра по
/// местному времени, со звуком - это не посреди игры, как трекер, а утренняя сводка.
/// </summary>
public class SendMorningDigestUseCase(
    IPlayerRepository players,
    IPlayerAlertPrefsRepository alertPrefs,
    IPlayerBattleRepository battles,
    IClanRepository clans,
    IClashRoyaleApi crApi,
    IMetaRepository meta,
    CollectPlayerBattlesUseCase collect,
    IServiceSettingRepository settings,
    INotificationSender sender,
    ISentNotificationRepository sentLog)
{
    /// <summary>Вид отметки в общей таблице отправленного - тот же, что у воркера.</summary>
    public const string SentKind = "morningdigest";

    /// <summary>Рубильник владельца: «off» - дайджест никому не уходит.</summary>
    public const string KillSwitchKey = "digest.dm";

    /// <summary>
    /// Окно отправки по местному времени: с 10:00 до 13:00. Не ровно в 10:00 - чтобы
    /// рестарт или долгий проход не лишал дайджеста; позже часа обеда «вчера» уже несвежо.
    /// </summary>
    public const int FromHourLocal = 10;
    public const int ToHourLocal = 13;

    /// <summary>Меньше трёх боёв - не о чем рассказывать.</summary>
    public const int MinBattles = 3;

    /// <summary>
    /// Сколько дайджестов за проход. У большинства один часовой пояс, и все они
    /// созревают в одну минуту; остальные уйдут следующими проходами того же окна.
    /// </summary>
    public const int MaxPerTick = 40;

    public record Summary(int Due, int Sent, int Skipped, int Blocked, int Failed);

    /// <summary>Учитываем ладдер и Путь легенд: война и турниры живут по своим правилам.</summary>
    public static bool Counts(PlayerBattle b) => MatchReport.ModeKey(b.Type) is "ladder" or "pol";

    /// <param name="sentKeys">Дедуп между проходами и рестартами: "tg|yyyy-MM-dd" (вчерашний местный день).</param>
    public async Task<Summary> ExecuteAsync(ISet<string> sentKeys, CancellationToken ct = default)
    {
        if (string.Equals(await settings.GetAsync(KillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase))
            return new Summary(0, 0, 0, 0, 0);

        var now = DateTime.UtcNow;
        var prefsByTg = (await alertPrefs.GetAllAsync(ct))
            .GroupBy(p => p.TelegramUserId)
            .ToDictionary(g => g.Key, g => g.First());
        // У человека бывает несколько строк игрока (по одной на клан) - пишем ему один раз.
        var users = (await players.GetAllLinkedAsync(ct))
            .Where(p => p.TelegramUserId is not null)
            .Select(p => p.TelegramUserId!.Value)
            .Distinct()
            .ToList();

        int due = 0, sent = 0, skipped = 0, blocked = 0, failed = 0;
        Dictionary<int, CrCatalogCard>? catalog = null;
        MatchupWindow.Data? window = null;
        var windowLoaded = false;
        var matchupCache = new Dictionary<(string, string), MatchupStats.Result>();

        foreach (var tg in users)
        {
            var prefs = prefsByTg.GetValueOrDefault(tg);
            if (prefs is not null && (prefs.DmBlocked || prefs.DigestOff)) continue;

            var tz = prefs?.TzOffsetMinutes ?? TiltMessages.DefaultTzOffsetMinutes;
            var local = now.AddMinutes(tz);
            if (local.Hour < FromHourLocal || local.Hour >= ToHourLocal) continue;

            var todayStart = TiltMessages.LocalMidnightUtc(now, tz);
            var from = todayStart.AddDays(-1);
            var key = $"{tg}|{local.Date.AddDays(-1):yyyy-MM-dd}";
            if (sentKeys.Contains(key)) continue;
            if (due >= MaxPerTick) break;
            due++;

            try
            {
                var player = await players.GetByTelegramIdAsync(tg, ct);
                if (player is null) { sentKeys.Add(key); skipped++; continue; }

                // Журнал за вечер мог ещё не доехать (общий сбор раз в три часа) - дочитываем.
                try { await collect.SyncAsync(player.PlayerTag, ct); }
                catch { /* API игры недоступен - считаем по тому, что уже есть */ }

                // С запасом в заход до полуночи: серия поражений, начатая в 23:50, не рвётся в 00:00.
                var history = (await battles.GetSinceAsync(player.PlayerTag, from - BattleAnalyzer.SessionGap, ct))
                    .Where(b => b.BattleTimeUtc < todayStart)
                    .ToList();
                var day = history.Where(b => b.BattleTimeUtc >= from && Counts(b)).ToList();
                if (day.Count < MinBattles)
                {
                    // Отметка и тут: иначе каждый проход окна перечитывал бы журнал заново.
                    sentKeys.Add(key);
                    skipped++;
                    continue;
                }

                catalog ??= await GetMetaDecksUseCase.SafeCatalogAsync(crApi, ct);
                if (!windowLoaded)
                {
                    windowLoaded = true;
                    try { window = await MatchupWindow.LoadAsync(meta, ct); }
                    catch { /* без меты причина «контр-колода» просто не сработает */ }
                }
                Dictionary<int, CrCatalogCard> cat = catalog;
                var win = window;
                MatchupStats.Result? Top(PlayerBattle b)
                {
                    if (win is null) return null;
                    var pair = (b.DeckKey, b.OppDeckKey);
                    if (!matchupCache.TryGetValue(pair, out var r))
                        matchupCache[pair] = r = MatchupStats.FromTop(b.DeckKey, b.OppDeckKey, win, cat);
                    return r;
                }

                var t = BotText.For(await TiltMessages.LangAsync(prefs, player, clans, ct));
                var text = DigestText.Morning(t, day, history, Top, cat);

                // Отметку занимаем до отправки: вторая копия воркера или перезапуск посреди
                // прохода иначе прислали бы ту же сводку ещё раз
                if (!await sentLog.TryClaimAsync(SentKind, key, ct)) { sentKeys.Add(key); continue; }

                var result = await sender.SendDmAsync(tg, text, DigestText.MorningButtons(t), silent: false, ct);
                if (!result.Delivered && !result.Blocked)
                {
                    // Временный сбой Telegram - отметку возвращаем, следующий проход попробует снова
                    try { await sentLog.ReleaseAsync(SentKind, key, ct); } catch { /* не вышло - значит, без повтора */ }
                }
                if (result.Delivered)
                {
                    sentKeys.Add(key);
                    sent++;
                }
                else if (result.Blocked)
                {
                    // Заблокировал - замолкаем до его сообщения боту, как и трекер.
                    sentKeys.Add(key);
                    blocked++;
                    var tracked = await alertPrefs.GetOrCreateAsync(tg, ct);
                    tracked.DmBlocked = true;
                    await alertPrefs.SaveChangesAsync(ct);
                }
                else failed++;   // временный сбой Telegram - следующий проход того же окна попробует снова
            }
            catch
            {
                // Один сломанный журнал не должен лишать дайджеста остальных.
                failed++;
            }
        }
        return new Summary(due, sent, skipped, blocked, failed);
    }
}
