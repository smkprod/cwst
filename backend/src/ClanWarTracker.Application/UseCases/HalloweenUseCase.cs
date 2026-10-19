using System.Globalization;
using System.Net;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// Хэллоуин: босс клана. Два босса подряд, у каждого своя военная неделя. Клан бьёт
/// босса победами в КВ, а привязанные игроки - ещё и победами в ладдере и Пути легенд.
///
/// Урон не хранится, а считается из журналов боёв на каждый запрос - как билеты
/// челленджа: в таблице ровно то, что было в игре, и подделать её нечем. Хранится
/// только здоровье босса: оно считается от размера клана один раз, при первом
/// подсчёте, и не прыгает посреди недели, когда кто-то ушёл или пришёл.
/// </summary>
public class HalloweenUseCase(
    IClanRepository clans,
    IPlayerRepository players,
    IWarBattleRepository warBattles,
    IPlayerBattleRepository battles,
    IServiceSettingRepository settings,
    ISentNotificationRepository sentLog,
    INotificationSender notifier,
    IChannelPublisher channel,
    ICardUrls cardUrls,
    PlusAccess plus,
    IMemoryCache cache)
{
    /// <param name="HpPerMember">Здоровья на активного игрока клана.</param>
    /// <param name="BonusHp">Надбавка сверх размера клана.</param>
    public sealed record Boss(string Key, DateTime StartUtc, DateTime EndUtc, int HpPerMember, int BonusHp = 0);

    /// <summary>
    /// Граница между боссами - понедельник после конца КВ: так каждому достаётся целая
    /// военная неделя (четверг-воскресенье), а не её половина.
    /// </summary>
    public static readonly Boss[] Bosses =
    [
        // +5000: вампир пришёл посреди КВ, и её последний день достался ему в урон
        new("vampire", Utc(2026, 10, 18, 21), Utc(2026, 10, 26, 12), 600, BonusHp: 5000),
        new("king", Utc(2026, 10, 26, 12), Utc(2026, 11, 2, 12), 700),
    ];

    /// <summary>Победа в КВ.</summary>
    public const int WarWin = 100;

    /// <summary>Победа в КВ с тремя коронами - вместо обычной.</summary>
    public const int WarThreeCrowns = 250;

    /// <summary>Победа в ладдере или Пути легенд - только у привязанных: их бои бот видит.</summary>
    public const int LadderWin = 30;

    /// <summary>
    /// Больше побед в ладдере за день не засчитываем: КВ ограничена четырьмя колодами в
    /// день, а ладдер нет, и без потолка босса добивал бы один любитель ночных сессий.
    /// </summary>
    public const int LadderWinsPerDay = 10;

    /// <summary>Всем привязанным, кто бил поверженного босса.</summary>
    public const int RewardPlusDays = 3;

    private const int MinMembers = 10;
    private const int MaxMembers = 50;

    /// <summary>У клана ещё нет журнала КВ (подключился только что) - считаем средним.</summary>
    private const int UnknownMembers = 20;

    private const int TzOffsetMinutes = 180;

    /// <summary>Отметки «уже отправлено/выдано» - навсегда: ивент один, повторять нечего.</summary>
    private const string Kind = SentNotification.OncePrefix + "hw";

    public static Boss? Live(DateTime nowUtc) => Bosses.FirstOrDefault(b => nowUtc >= b.StartUtc && nowUtc < b.EndUtc);

    public static Boss? Next(DateTime nowUtc) => Bosses.FirstOrDefault(b => b.StartUtc > nowUtc);

    public static Boss? Before(Boss boss) => Array.IndexOf(Bosses, boss) is var i and > 0 ? Bosses[i - 1] : null;

    public static DateTime EventEndUtc => Bosses[^1].EndUtc;

    public sealed record Hitter(string Tag, string Name, int Damage, int WarWins, int ThreeCrowns, int LadderWins);

    public sealed record Progress(Boss Boss, int Hp, int Damage, int Today, DateTime? DefeatedUtc, List<Hitter> Hitters)
    {
        public bool Defeated => DefeatedUtc is not null;
        public int Left => Math.Max(0, Hp - Damage);
    }

    /// <summary>Здоровье босса: активные игроки клана (10-50) на здоровье за каждого, кратно 500.</summary>
    public static int HpFor(Boss boss, int activeMembers)
    {
        var n = activeMembers <= 0 ? UnknownMembers : Math.Clamp(activeMembers, MinMembers, MaxMembers);
        var raw = n * boss.HpPerMember;
        return (raw + 499) / 500 * 500;
    }

    /// <summary>
    /// Урон по боссу из боёв клана. КВ - бои любого участника, ладдер и Путь легенд -
    /// только тех, чьи бои бот собирает (привязанных). Бои КВ привязанных лежат в обоих
    /// журналах, поэтому из личного берём только ладдер и Путь легенд.
    /// </summary>
    public static Progress Tally(Boss boss, int hp, IEnumerable<WarBattle> war, IEnumerable<PlayerBattle> own,
        IReadOnlyDictionary<string, string> names, DateTime todayStartUtc)
    {
        var hits = new List<(string Tag, DateTime At, int Damage, char Kind)>();
        var nameOf = new Dictionary<string, string>(names, StringComparer.OrdinalIgnoreCase);

        foreach (var b in war)
        {
            if (!b.Won || b.BattleTimeUtc < boss.StartUtc || b.BattleTimeUtc >= boss.EndUtc) continue;
            var three = b.CrownsFor >= 3;
            hits.Add((b.PlayerTag, b.BattleTimeUtc, three ? WarThreeCrowns : WarWin, three ? '3' : 'w'));
            nameOf.TryAdd(b.PlayerTag, b.PlayerName);
        }

        var ladder = own
            .Where(b => b.Result > 0 && b.BattleTimeUtc >= boss.StartUtc && b.BattleTimeUtc < boss.EndUtc
                        && MatchReport.ModeKey(b.Type) is "ladder" or "pol")
            .GroupBy(b => (Tag: b.PlayerTag.ToUpperInvariant(), Day: b.BattleTimeUtc.AddMinutes(TzOffsetMinutes).Date));
        foreach (var day in ladder)
            foreach (var b in day.OrderBy(b => b.BattleTimeUtc).Take(LadderWinsPerDay))
                hits.Add((b.PlayerTag, b.BattleTimeUtc, LadderWin, 'l'));

        hits.Sort((a, b) => a.At.CompareTo(b.At));
        int total = 0, today = 0;
        DateTime? defeated = null;
        foreach (var h in hits)
        {
            total += h.Damage;
            if (h.At >= todayStartUtc) today += h.Damage;
            if (defeated is null && total >= hp) defeated = h.At;
        }

        var hitters = hits
            .GroupBy(h => h.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Hitter(g.Key, nameOf.GetValueOrDefault(g.Key) ?? g.Key, g.Sum(h => h.Damage),
                g.Count(h => h.Kind == 'w'), g.Count(h => h.Kind == '3'), g.Count(h => h.Kind == 'l')))
            .OrderByDescending(h => h.Damage)
            .ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new Progress(boss, hp, total, today, defeated, hitters);
    }

    /// <summary>Полночь сегодня по Киеву, в UTC.</summary>
    public static DateTime TodayStartUtc(DateTime nowUtc) =>
        DateTime.SpecifyKind(nowUtc.AddMinutes(TzOffsetMinutes).Date.AddMinutes(-TzOffsetMinutes), DateTimeKind.Utc);

    /// <summary>
    /// Прогресс клана по боссу. Две минуты из кэша: бои подъезжают раз в полчаса, а
    /// вкладку открывают куда чаще.
    /// </summary>
    public async Task<Progress> ProgressAsync(Clan clan, Boss boss, CancellationToken ct = default, bool fresh = false)
    {
        var key = $"hw:{boss.Key}:{clan.Id}";
        if (!fresh && cache.TryGetValue(key, out Progress? hit) && hit is not null) return hit;

        var hp = await HpAsync(clan, boss, ct);
        var war = await warBattles.GetSinceAsync(clan.Id, boss.StartUtc, ct);
        var linked = (await players.GetByClanIdAsync(clan.Id, ct)).Where(p => p.TelegramUserId is not null).ToList();
        var own = linked.Count == 0
            ? []
            : await battles.GetForTagsBetweenAsync(linked.Select(p => p.PlayerTag).Distinct().ToList(),
                boss.StartUtc, boss.EndUtc, ct);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in linked) names.TryAdd(p.PlayerTag, p.Name);

        var progress = Tally(boss, hp, war, own, names, TodayStartUtc(DateTime.UtcNow));
        cache.Set(key, progress, new MemoryCacheEntryOptions
        {
            Size = 2 + progress.Hitters.Count / 20,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
        });
        return progress;
    }

    /// <summary>
    /// Здоровье босса для клана: сколько человек играли КВ за три недели до босса.
    /// Запоминается при первом подсчёте - дальше то же число до конца босса. Хранится
    /// без надбавки: её можно поменять, и она дойдёт и до уже посчитанных кланов.
    /// </summary>
    private async Task<int> HpAsync(Clan clan, Boss boss, CancellationToken ct)
    {
        var key = $"hw.hp.{boss.Key}.{clan.Id}";
        if (int.TryParse(await settings.GetAsync(key, ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stored)
            && stored > 0)
            return stored + boss.BonusHp;

        var recent = await warBattles.GetSinceAsync(clan.Id, boss.StartUtc.AddDays(-21), ct);
        var active = recent
            .Where(b => b.BattleTimeUtc < boss.StartUtc)
            .Select(b => b.PlayerTag.ToUpperInvariant())
            .Distinct()
            .Count();
        var hp = HpFor(boss, active);
        await settings.SetAsync(key, hp.ToString(CultureInfo.InvariantCulture), ct);
        return hp + boss.BonusHp;
    }

    public sealed record HitterDto(string Tag, string Name, int Damage, int WarWins, int ThreeCrowns, int LadderWins);

    public sealed record PastDto(string Key, int Hp, int Damage, DateTime? DefeatedUtc);

    public sealed record ViewDto(
        string ClanName, string BossKey, DateTime StartUtc, DateTime EndUtc, DateTime EventEndUtc,
        int Hp, int Damage, int Today, DateTime? DefeatedUtc,
        List<HitterDto> Top, HitterDto? Me, int? MyRank, int Hitters,
        string? NextBossKey, DateTime? NextStartUtc, PastDto? Previous,
        int WarWin, int WarThreeCrowns, int LadderWin, int LadderWinsPerDay, int RewardPlusDays);

    public const int TopShown = 30;

    /// <summary>Экран вкладки «Хэллоуин» для клана. null - ивент не идёт.</summary>
    public async Task<ViewDto?> ViewAsync(Clan clan, string? myTag, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (Live(now) is not { } boss) return null;

        var p = await ProgressAsync(clan, boss, ct);
        PastDto? previous = null;
        if (Before(boss) is { } prev)
        {
            var pp = await ProgressAsync(clan, prev, ct);
            previous = new PastDto(prev.Key, pp.Hp, pp.Damage, pp.DefeatedUtc);
        }

        static HitterDto Dto(Hitter h) => new(h.Tag, h.Name, h.Damage, h.WarWins, h.ThreeCrowns, h.LadderWins);
        var myIndex = myTag is null ? -1 : p.Hitters.FindIndex(h => string.Equals(h.Tag, myTag, StringComparison.OrdinalIgnoreCase));
        var next = Next(now);
        return new ViewDto(
            clan.Name, boss.Key, boss.StartUtc, boss.EndUtc, EventEndUtc,
            p.Hp, p.Damage, p.Today, p.DefeatedUtc,
            p.Hitters.Take(TopShown).Select(Dto).ToList(),
            myIndex >= 0 ? Dto(p.Hitters[myIndex]) : null, myIndex >= 0 ? myIndex + 1 : null, p.Hitters.Count,
            next?.Key, next?.StartUtc, previous,
            WarWin, WarThreeCrowns, LadderWin, LadderWinsPerDay, RewardPlusDays);
    }

    /// <summary>
    /// Проход воркера: анонс босса в чаты кланов и канал, «босс повержен» и Плюс в награду.
    /// Сообщения - только днём по Киеву: ночью их никто не прочтёт, а будильник из чата
    /// клана ради ивента никому не нужен. Возвращает, сколько сообщений ушло.
    /// </summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var live = Live(now);
        // Закончившийся за последние сутки: бои КВ доезжают с опозданием, и добить его
        // могли в последний час - награда должна дойти и тогда
        var ended = Bosses.LastOrDefault(b => b.EndUtc <= now && now - b.EndUtc < TimeSpan.FromHours(24));
        if (live is null && ended is null) return 0;

        var hour = now.AddMinutes(TzOffsetMinutes).Hour;
        var daytime = hour is >= 10 and < 22;
        var done = await sentLog.GetKeysAsync(Kind, DateTime.MinValue, ct);
        var sent = 0;

        if (live is not null && daytime && !done.Contains($"{live.Key}:channel"))
            sent += await AnnounceChannelAsync(live, ct) ? 1 : 0;

        foreach (var clan in await clans.GetAllAsync(ct))
        {
            foreach (var boss in new[] { ended, live })
            {
                if (boss is null) continue;
                Progress p;
                try { p = await ProgressAsync(clan, boss, ct, fresh: true); }
                catch (Exception) when (!ct.IsCancellationRequested) { continue; }

                if (p.Defeated) await RewardAsync(clan, p, done, ct);
                if (clan.TelegramChatId == 0 || !daytime) continue;

                var t = HalloweenText.For(NotificationSettings.Parse(clan.NotificationSettingsJson).Language);
                if (boss == live && !done.Contains($"{boss.Key}:start:{clan.Id}")
                    && await ClaimAsync($"{boss.Key}:start:{clan.Id}", done, ct))
                {
                    var prevLine = "";
                    if (Before(boss) is { } prev)
                    {
                        var pp = await ProgressAsync(clan, prev, ct);
                        prevLine = pp.Defeated
                            ? string.Format(t.PrevDefeated, t.Name(prev.Key))
                            : string.Format(t.PrevEscaped, t.Name(prev.Key), Percent(pp.Left, pp.Hp));
                    }
                    var text = string.Format(t.Start, t.Name(boss.Key), Num(p.Hp), DateText(boss.EndUtc, t),
                        Num(WarWin), Num(WarThreeCrowns), Num(LadderWin), RewardPlusDays) + prevLine;
                    if (await SendAsync(clan, boss, text, t, ct)) sent++;
                    else await sentLog.ReleaseAsync(Kind, $"{boss.Key}:start:{clan.Id}", ct);
                }

                if (p.Defeated && !done.Contains($"{boss.Key}:kill:{clan.Id}")
                    && await ClaimAsync($"{boss.Key}:kill:{clan.Id}", done, ct))
                {
                    var medals = new[] { "🥇", "🥈", "🥉" };
                    var top = string.Join("\n", p.Hitters.Take(3).Select((h, i) => $"{medals[i]} {h.Name} — {Num(h.Damage)}"));
                    var text = string.Format(t.Killed, t.Name(boss.Key), top, RewardPlusDays);
                    if (await SendAsync(clan, boss, text, t, ct)) sent++;
                    else await sentLog.ReleaseAsync(Kind, $"{boss.Key}:kill:{clan.Id}", ct);
                }
            }
        }
        return sent;
    }

    /// <summary>
    /// Плюс всем привязанным, кто бил поверженного босса. Отметка на человека и босса:
    /// перешёл в другой клан, который тоже добил того же босса, - награда одна.
    /// </summary>
    private async Task RewardAsync(Clan clan, Progress p, HashSet<string> done, CancellationToken ct)
    {
        var linked = (await players.GetByClanIdAsync(clan.Id, ct))
            .Where(x => x.TelegramUserId is not null)
            .GroupBy(x => x.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var h in p.Hitters)
        {
            if (h.Damage <= 0 || !linked.TryGetValue(h.Tag, out var player)) continue;
            var tg = player.TelegramUserId!.Value;
            var key = $"{p.Boss.Key}:reward:{tg}";
            if (done.Contains(key) || !await ClaimAsync(key, done, ct)) continue;
            try { await plus.GrantAsync(tg, RewardPlusDays, Entitlement.Sources.Grant, player.PlayerTag, 0, null, ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { await sentLog.ReleaseAsync(Kind, key, ct); }
        }
    }

    private async Task<bool> ClaimAsync(string key, HashSet<string> done, CancellationToken ct)
    {
        done.Add(key);
        return await sentLog.TryClaimAsync(Kind, key, ct);
    }

    /// <summary>Картинка босса с подписью и кнопкой во вкладку; картинка не дошла - текстом.</summary>
    private async Task<bool> SendAsync(Clan clan, Boss boss, string text, HalloweenText t, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<BotButton>> rows = [[new BotButton(t.Button, Url: "startapp:halloween")]];
        if (cardUrls.Asset($"hw/card-{boss.Key}.jpg") is { } photo
            && await notifier.SendPhotoToChatWithButtonsAsync(clan.TelegramChatId, photo, text, rows,
                clan.TelegramMessageThreadId, ct))
            return true;
        return await notifier.SendToChatWithButtonsAsync(clan.TelegramChatId, text, rows, clan.TelegramMessageThreadId, ct);
    }

    /// <summary>Пост в канал бота о новом боссе - один на босса. Канала нет - молча ничего.</summary>
    private async Task<bool> AnnounceChannelAsync(Boss boss, CancellationToken ct)
    {
        if (!long.TryParse(await settings.GetAsync(ChannelUseCase.KeyId, ct), out var channelId) || channelId == 0)
            return false;
        if (!await sentLog.TryClaimAsync(Kind, $"{boss.Key}:channel", ct)) return false;

        var t = HalloweenText.For("ru");
        var html = string.Format(t.Channel, WebUtility.HtmlEncode(t.Name(boss.Key)), DateText(boss.EndUtc, t),
            Num(WarWin), Num(WarThreeCrowns), Num(LadderWin), RewardPlusDays);
        IReadOnlyList<IReadOnlyList<BotButton>> rows = [[new BotButton(t.Button, Url: "startapp:halloween")]];
        var id = await channel.PublishAsync(channelId, html, cardUrls.Asset($"hw/card-{boss.Key}.jpg"), rows, ct)
                 ?? await channel.PublishAsync(channelId, html, null, rows, ct);
        if (id is null) await sentLog.ReleaseAsync(Kind, $"{boss.Key}:channel", ct);
        return id is not null;
    }

    private static string Num(int n) => n.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');

    private static int Percent(int part, int whole) => whole <= 0 ? 0 : (int)Math.Round(100.0 * part / whole);

    /// <summary>Последний день босса по Киеву: конец в понедельник днём - значит, «до понедельника».</summary>
    private static string DateText(DateTime endUtc, HalloweenText t)
    {
        var local = endUtc.AddMinutes(TzOffsetMinutes);
        return string.Format(t.Date, local.Day, t.Months[local.Month - 1]);
    }

    private static DateTime Utc(int y, int m, int d, int h) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);
}
