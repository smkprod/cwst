using System.Net;
using System.Text.RegularExpressions;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ClanWarTracker.Application.UseCases;

/// <summary>Когда бот напоминает в чат: заранее, последний звонок или глава нажал «пнуть».</summary>
public enum PepMoment { Reminder, FinalCall, Nudge }

/// <summary>
/// Напутствие к напоминанию в чат клана: вместо сухого «ещё не доиграли» - пара фраз
/// от нейросети по цифрам этого дня, с напором и моралью. Цель - чтобы недоигравшим
/// стало неудобно перед своими: сколько уже отыграло, сколько медалей висит в воздухе,
/// кто дышит в спину в гонке.
///
/// Граница жёсткая: никого не называть (список с тегами бот добавит сам), не оскорблять,
/// не унижать и не грозить исключением - стыд через ответственность перед командой, а не
/// через травлю. Тон выбирает глава: спокойно, с напором (по умолчанию) или жёстко.
///
/// Нейросеть не ответила или ответ странный - уходит прежний шаблон, напоминание не теряется.
/// </summary>
public class WarPepTalkUseCase(INewsTranslator ai, IServiceSettingRepository settings, IMemoryCache cache)
{
    /// <summary>
    /// Рубильник владельца в панели канала: «off» - напоминания снова шаблоном. По умолчанию
    /// включено: напоминание и так уходит в чат, нейросеть меняет только первые строки.
    /// </summary>
    public const string KillSwitchKey = "channel.auto.remindai";

    /// <param name="roster">Текущий состав клана в войне - теги.</param>
    /// <returns>Готовый к HTML текст напутствия; null - писать шаблоном.</returns>
    public async Task<string?> IntroAsync(Clan clan, NotificationSettings ns, WarStatus war,
        IReadOnlyCollection<string> roster, TimeSpan timeLeft, PepMoment moment, CancellationToken ct = default)
    {
        if (!ai.Enabled) return null;
        try
        {
            if (string.Equals(await settings.GetAsync(KillSwitchKey, ct), "off", StringComparison.OrdinalIgnoreCase)) return null;
        }
        catch { /* настройка не прочиталась - считаем включённым */ }

        var facts = Facts.From(war, roster, timeLeft);
        if (facts.Slackers == 0) return null;

        // Одно и то же напоминание (тот же клан, день и момент) не пишем заново - глава
        // может нажать «пнуть» дважды за минуту, а запрос стоит денег
        var key = $"pep:{clan.Id}:{war.SeasonId}:{war.SectionIndex}:{war.PeriodIndex}:{moment}:{DateTime.UtcNow:yyyyMMddHH}";
        if (cache.TryGetValue(key, out string? hit) && hit is not null) return hit;

        var tone = NotificationSettings.ParseTone(ns.ReminderTone);
        var prompt = Prompt(clan.Name, ns.Language, tone, moment, facts, Angle(clan.Id, war.PeriodIndex, moment));
        string? text;
        try { text = await ai.ChatAsync(prompt, 220, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }

        var clean = Clean(text);
        if (clean is null) return null;
        cache.Set(key, clean, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });
        return clean;
    }

    /// <summary>Цифры дня, из которых нейросеть пишет - и больше ни из чего.</summary>
    internal record Facts(
        int Played, int Slackers, int DecksMissing, int MedalsAtStake, int Hours, int Minutes,
        int Day, int Place, int Clans, int GapAhead, int GapBehind)
    {
        public static Facts From(WarStatus war, IReadOnlyCollection<string> roster, TimeSpan timeLeft)
        {
            var mine = war.Participants.Where(p => roster.Contains(p.PlayerTag)).ToList();
            var slackers = mine.Where(p => p.DecksUsedToday < 4).ToList();
            var missing = slackers.Sum(p => 4 - p.DecksUsedToday);

            // Сколько медалей клан обычно берёт за колоду сегодня; без данных - средний 150
            var us = war.RaceClans.FirstOrDefault(c => string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
            var perDeck = us is { DecksUsedToday: > 0 } ? (double)us.TodayFame / us.DecksUsedToday : 150;
            perDeck = Math.Clamp(perDeck, 100, 225);

            var order = war.RaceClans.OrderByDescending(c => c.Fame).ToList();
            var place = order.FindIndex(c => string.Equals(c.Tag, war.ClanTag, StringComparison.OrdinalIgnoreCase));
            var gapAhead = place > 0 ? order[place - 1].Fame - order[place].Fame : 0;
            var gapBehind = place >= 0 && place < order.Count - 1 ? order[place].Fame - order[place + 1].Fame : 0;

            var left = timeLeft < TimeSpan.Zero ? TimeSpan.Zero : timeLeft;
            return new Facts(
                Played: mine.Count(p => p.DecksUsedToday >= 4),
                Slackers: slackers.Count,
                DecksMissing: missing,
                MedalsAtStake: (int)Math.Round(missing * perDeck / 50) * 50,
                Hours: (int)left.TotalHours,
                Minutes: left.Minutes,
                Day: Math.Clamp(war.PeriodIndex - 2, 1, 4),
                Place: place + 1,
                Clans: order.Count,
                GapAhead: gapAhead,
                GapBehind: gapBehind);
        }
    }

    private static readonly string[] Angles =
    [
        "сравни с теми, кто уже отыграл: они сделали свою часть за себя и за клан",
        "переведи недоигранные колоды в медали, которые клан теряет прямо сейчас",
        "напомни про место в гонке и отрыв - соперники свои колоды доиграют",
        "война командная: каждый, кто не отыграл, перекладывает работу на других",
        "времени ещё хватает, а оправданий не будет - четыре боя это пара минут",
    ];

    /// <summary>Угол меняется по дню и моменту - чтобы чат не читал каждый раз одно и то же.</summary>
    private static string Angle(int clanId, int day, PepMoment moment) =>
        Angles[Math.Abs(HashCode.Combine(clanId, day, (int)moment, DateTime.UtcNow.DayOfYear)) % Angles.Length];

    internal static string Prompt(string clanName, string? language, string tone, PepMoment moment, Facts f, string angle)
    {
        var lang = language switch { "uk" => "українською мовою", "en" => "in English", _ => "по-русски" };
        var toneRule = tone switch
        {
            "calm" => "Тон спокойный и дружеский: просто напомни и объясни, почему это важно клану.",
            "hard" => "Тон жёсткий и прямой, без смягчений и смайликов-улыбок: команда тянет, а эти игроки - нет. "
                      + "Можно сказать, что глава видит, кто подводит клан. Но без оскорблений и унижения.",
            _ => "Тон с напором и моралью: недоигравшим должно стать неловко перед теми, кто уже отыграл, "
                 + "через ответственность перед командой. Уважительно, без оскорблений.",
        };
        var momentRule = moment switch
        {
            PepMoment.FinalCall => "Это последний звонок: до конца дня около 30 минут, после - колоды сгорят.",
            PepMoment.Nudge => "Это глава клана лично попросил бота поторопить недоигравших.",
            _ => "Это плановое напоминание за несколько часов до конца военного дня.",
        };
        var race = f.Place > 0 && f.Clans > 1
            ? $"Место клана в гонке: {f.Place} из {f.Clans}"
              + (f.GapAhead > 0 ? $", до клана впереди {f.GapAhead} медалей" : "")
              + (f.GapBehind > 0 ? $", клан позади отстаёт на {f.GapBehind}" : "") + "."
            : "Место в гонке неизвестно.";
        return $"""
            Ты пишешь короткое вступление к напоминанию в Telegram-чате клана Clash Royale «{Safe(clanName)}» про клановую войну.
            Сразу после твоего текста бот сам выведет список недоигравших с тегами - поэтому никого не называй.
            {momentRule}
            {toneRule}
            Подача: {angle}.

            Цифры (используй только их, ничего не придумывай):
            - День войны {f.Day} из 4, до конца дня {f.Hours} ч {f.Minutes} мин.
            - Уже отыграли все 4 колоды: {f.Played} чел. Не доиграли: {f.Slackers} чел., не сыграно колод: {f.DecksMissing}.
            - Это примерно {f.MedalsAtStake} медалей, которые клан может недополучить.
            - {race}

            Правила: пиши {lang}; 2-3 коротких предложения, не длиннее 300 символов; одно эмодзи в начале;
            без имён, ников и @; без мата, оскорблений, насмешек над людьми и угроз исключить из клана;
            без markdown, без кавычек вокруг всего текста.
            """;
    }

    /// <summary>Чистим ответ: без разметки, без упоминаний, в разумной длине; HTML экранируем.</summary>
    internal static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = Regex.Replace(text, @"(\*\*|__|`|^#+\s*)", "", RegexOptions.Multiline).Trim().Trim('"', '«', '»').Trim();
        // Упоминаний от нейросети быть не должно: тегает только бот, по привязанным аккаунтам
        t = Regex.Replace(t, @"@\w+", "").Replace("  ", " ").Trim();
        if (t.Length is < 40 or > 500) return null;
        return WebUtility.HtmlEncode(t);
    }

    private static string Safe(string s) => Regex.Replace(s, @"[\r\n«»""]", " ").Trim();
}
