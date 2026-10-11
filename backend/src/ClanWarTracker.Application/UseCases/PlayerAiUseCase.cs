using System.Text;
using ClanWarTracker.Application.Battles;
using ClanWarTracker.Application.Notifications;
using ClanWarTracker.Domain.Entities;
using ClanWarTracker.Domain.Interfaces;

namespace ClanWarTracker.Application.UseCases;

/// <summary>
/// ИИ-помощник игрока: разбор поражений, тренер в личке бота и разбор колоды.
///
/// Нейросеть не лезет в базу сама: бот собирает о человеке короткую сводку - профиль,
/// колода с уровнями, последние бои с причинами поражений, неудобные карты соперников,
/// мета топ-1000 - и модель отвечает только по ней. Поэтому советы конкретные («держи
/// Log под Goblin Barrel»), а не общие слова из интернета.
///
/// У каждой функции дневной лимит: бесплатно - попробовать, с Плюсом - пользоваться.
/// Запрос копеечный, но без потолка один энтузиаст гонял бы его сутки напролёт.
/// </summary>
public class PlayerAiUseCase(
    INewsTranslator ai,
    IAiUsageRepository usage,
    PlusAccess plus,
    IPlayerRepository players,
    IPlayerBattleRepository battles,
    CollectPlayerBattlesUseCase collect,
    IClashRoyaleApi crApi,
    GetTopMetaUseCase meta,
    IPlayerAlertPrefsRepository alertPrefs,
    IClanRepository clans)
{
    public const string Coach = "coach";
    public const string Analysis = "analysis";
    public const string Deck = "deck";

    /// <summary>Сколько раз в день: бесплатно / с Плюсом.</summary>
    public static readonly IReadOnlyDictionary<string, (int Free, int Plus)> Limits = new Dictionary<string, (int, int)>
    {
        [Coach] = (3, 30),
        [Analysis] = (1, 10),
        [Deck] = (1, 10),
    };

    private const int TzOffsetMinutes = 180;

    public enum Status { Ok, NoAi, NotLinked, Limit, Failed, Empty }

    /// <param name="Text">Ответ в простой разметке: строки, **жирный**. Бот переводит её в HTML Telegram, приложение - в свою вёрстку.</param>
    /// <param name="Left">Сколько ещё осталось сегодня.</param>
    /// <param name="Plus">У человека Плюс - для подсказки «с Плюсом больше».</param>
    /// <param name="Topic">О чём вопрос тренеру: deck - про колоду и бои игрока (к ответу - картинка колоды), game, other.</param>
    public record Answer(Status Status, string? Text, int Left, int Limit, bool Plus, int PlusLimit, string Topic = "game",
        string? PlayerTag = null);

    public static string Today(DateTime nowUtc) => nowUtc.AddMinutes(TzOffsetMinutes).ToString("yyyy-MM-dd");

    /// <summary>
    /// Общие правила вида ответа. Сплошной абзац с телефона не читают: короткий заголовок,
    /// пункты по строке с эмодзи, жирным - только заголовок и названия карт, в конце -
    /// одно действие. Это и делает совет полезным, а не «умным».
    /// </summary>
    private const string Style = """
        Оформление (строго):
        - короткие строки, никаких сплошных абзацев; каждый пункт - отдельная строка, начинается с эмодзи;
        - **жирным** выделяй только заголовки и названия карт; больше никакой разметки (без решёток, без списков «-»);
        - пустая строка между блоками;
        - на «ты», дружелюбно, без воды и вступлений вроде «Отличный вопрос».
        """;

    /* ---------------- Функции ---------------- */

    /// <summary>Разбор последних поражений: что повторяется и что с этим делать.</summary>
    public Task<Answer> AnalyzeLossesAsync(long tg, string? lang, CancellationToken ct = default) =>
        RunAsync(tg, Analysis, lang, needPlayer: true, async (player, l) =>
        {
            var data = await PlayerDataAsync(player!, ct);
            return Prompt(l, """
                Ты опытный тренер по Clash Royale. Ниже данные игрока: профиль, колода, последние бои и причины поражений.
                Найди 2-3 самые частые и исправимые причины поражений и дай по каждой конкретный совет с картами.
                Если причина - уровни карт, скажи, какую карту качать первой. Если тильт - предложи правило остановки.

                Формат ответа:
                **🔍 Что мешает побеждать**
                <эмодзи> **Причина** - коротко, с цифрой из данных (2-3 строки)

                **✅ Что делать**
                <эмодзи> **Карта или приём** - конкретный совет в одну фразу (2-3 строки)

                👉 **Главное:** одно действие на следующий бой.

                Весь ответ до 700 символов. Только факты из данных, ничего не выдумывай.
                """ + "\n" + Style, data);
        }, ct);

    /// <summary>Свободный вопрос тренеру из лички бота. Без привязанного тега - отвечает по мете.</summary>
    /// <param name="telegramLang">Язык Telegram - для тех, у кого нет ни выбранного языка, ни клана.</param>
    public Task<Answer> AskCoachAsync(long tg, string question, string? telegramLang, CancellationToken ct = default) =>
        RunAsync(tg, Coach, null, false, async (player, l) =>
        {
            var data = player is null
                ? "Игрок не привязал тег - личных данных нет, отвечай по мете и общим принципам.\n" + await MetaDataAsync(ct)
                : await PlayerDataAsync(player, ct);
            var q = question.Length > 800 ? question[..800] : question;
            return Prompt(l, $$"""
                Ты тренер по Clash Royale в личке Telegram-бота Clanify. Игрок прислал сообщение.
                Сначала определи тему: "deck" - про его колоду, карты, контры или бои; "game" - другой вопрос про Clash Royale;
                "other" - не про игру (погода, жизнь, шутки).

                Если "other": text - ОДНА короткая дружелюбная фраза, что ты тренер по Clash Royale, и пример вопроса.
                Никакого разбора колоды и советов.

                Иначе text строго в формате:
                **<эмодзи> Заголовок до 6 слов**

                <эмодзи> **Карта или тема** - одна короткая фраза до 15 слов
                (2-4 таких строки)

                👉 одно главное действие на следующий бой

                Весь text до 500 символов. Опирайся на данные игрока и мету, ничего не выдумывай.

                Ответ строго JSON без пояснений: {"topic": "deck|game|other", "text": "..."}

                Сообщение игрока: {{q}}
                """ + "\n" + Style, data);
        }, ct, telegramLang, json: true);

    /// <summary>Разбор колоды из подборки: почему она подходит и как ей играть.</summary>
    public Task<Answer> ExplainDeckAsync(long tg, string deckName, IReadOnlyList<string> cards, string? lang,
        CancellationToken ct = default) =>
        RunAsync(tg, Deck, lang, needPlayer: true, async (player, l) =>
        {
            var data = await PlayerDataAsync(player!, ct);
            var list = string.Join(", ", cards.Take(8));
            return Prompt(l, $"""
                Ты тренер по Clash Royale. Игрок смотрит колоду «{deckName}»: {list}.
                По данным игрока (уровни его карт, какими колодами он уже побеждает, мета) разбери её.

                Формат ответа:
                **🃏 Подходит ли тебе**
                одна-две строки: да/нет и почему - по уровням и стилю

                **⚔️ План игры**
                🗡 атака - одна фраза
                🛡 защита - одна фраза

                **🎯 Ключевые розыгрыши**
                <эмодзи> **Карта + Карта** - зачем (2 строки)

                ⚠️ Осторожно с: **карта**, **карта**
                ⬆️ Качать первой: **карта** (если есть отстающие)

                Весь ответ до 800 символов. Ничего не выдумывай.
                """ + "\n" + Style, data);
        }, ct);

    /* ---------------- Общий ход ---------------- */

    private async Task<Answer> RunAsync(long tg, string feature, string? lang, bool needPlayer,
        Func<Player?, string, Task<string>> buildPrompt, CancellationToken ct, string? fallbackLang = null, bool json = false)
    {
        var limits = Limits[feature];
        var status = await plus.GetAsync(tg, ct);
        var isPlus = status.Active;
        var limit = isPlus ? limits.Plus : limits.Free;
        if (!ai.Enabled) return new Answer(Status.NoAi, null, 0, limit, isPlus, limits.Plus);

        var player = await players.GetByTelegramIdAsync(tg, ct);
        if (needPlayer && player is null) return new Answer(Status.NotLinked, null, 0, limit, isPlus, limits.Plus);

        var day = Today(DateTime.UtcNow);
        if (!await usage.TryUseAsync(tg, feature, day, limit, ct))
            return new Answer(Status.Limit, null, 0, limit, isPlus, limits.Plus);

        var l = await LangAsync(tg, player, lang, fallbackLang, ct);
        string? raw;
        try
        {
            var prompt = await buildPrompt(player, l);
            raw = json ? await ai.ChatJsonAsync(prompt, 900, ct) : await ai.ChatAsync(prompt, 1200, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { raw = null; }

        var (topic, text) = json ? ParseTopic(raw) : ("game", raw);
        var used = await usage.CountAsync(tg, feature, day, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            // Нейросеть не ответила - попытку возвращаем: человек за сбой платить не должен
            await usage.RefundAsync(tg, feature, day, ct);
            return new Answer(Status.Failed, null, Math.Max(0, limit - used + 1), limit, isPlus, limits.Plus);
        }
        // Вопрос не про игру - не повод списывать попытку
        if (topic == "other")
        {
            await usage.RefundAsync(tg, feature, day, ct);
            used = Math.Max(0, used - 1);
        }
        return new Answer(Status.Ok, Clean(text), Math.Max(0, limit - used), limit, isPlus, limits.Plus, topic,
            player?.PlayerTag);
    }

    /// <summary>{"topic": "...", "text": "..."} → (тема, текст). Не JSON - весь ответ считаем текстом про игру.</summary>
    private static (string Topic, string? Text) ParseTopic(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("game", null);
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw[start..(end + 1)]);
                var root = doc.RootElement;
                var topic = root.TryGetProperty("topic", out var t) ? t.GetString() : null;
                var text = root.TryGetProperty("text", out var x) ? x.GetString() : null;
                return (topic is "deck" or "game" or "other" ? topic : "game", text);
            }
            catch (System.Text.Json.JsonException) { }
        }
        return ("game", raw);
    }

    /// <summary>Жирный оставляем - его покажут и бот, и приложение; заголовки «#» модель вставляет зря.</summary>
    private static string Clean(string text) =>
        text.Replace("### ", "").Replace("## ", "").Replace("__", "").Trim();

    private static string Prompt(string lang, string task, string data)
    {
        var language = lang switch { "uk" => "українською мовою", "en" => "in English", _ => "по-русски" };
        return $"{task}\n\nОтвечай {language}. Названия карт - по-английски, как в игре.\n\nДанные:\n{data}";
    }

    /// <summary>Язык ответа: выбранный в приложении, затем язык клана, затем язык Telegram.</summary>
    private async Task<string> LangAsync(long tg, Player? player, string? wire, string? fallback, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(wire)) return TiltMessages.Code(BotText.ParseLang(wire));
        if (player is null) return TiltMessages.Code(BotText.ParseLang(fallback));
        try { return await TiltMessages.LangAsync(await alertPrefs.GetAsync(tg, ct), player, clans, ct); }
        catch { return "ru"; }
    }

    /* ---------------- Данные ---------------- */

    private static readonly Dictionary<string, string> ReasonText = new()
    {
        [LossReasons.Levels] = "карты соперника выше уровнем",
        [LossReasons.Matchup] = "неудобный матчап - колода соперника контрит",
        [LossReasons.Tilt] = "поражение на тильте после серии проигрышей",
        [LossReasons.Leak] = "много эликсира утекло впустую",
        [LossReasons.Close] = "равная игра, проиграно по мелочи",
        [LossReasons.Outplayed] = "переигран при равных условиях",
    };

    /// <summary>
    /// Сводка об игроке для модели: профиль, текущая колода с уровнями, итоги 30 дней,
    /// последние поражения с причинами, неудобные карты соперников, свои колоды, мета.
    /// Каждый кусок собирается отдельно: не открылся профиль - остаются бои, и наоборот.
    /// </summary>
    private async Task<string> PlayerDataAsync(Player player, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var names = await CardNamesAsync(ct);

        await Part(sb, async () =>
        {
            var info = await crApi.GetPlayerInfoAsync(player.PlayerTag, ct);
            if (info is null) return;
            sb.AppendLine($"Игрок: {info.Name}, кубки {info.Trophies} (рекорд {info.BestTrophies}), арена {info.ArenaName}, уровень короля {info.ExpLevel}.");
            if (info.CurrentPathOfLegend is { } pol && pol.LeagueNumber > 0)
                sb.AppendLine($"Путь легенд: лига {pol.LeagueNumber}, рейтинг {pol.Trophies}.");
            if (info.CurrentDeck.Count > 0)
                sb.AppendLine("Текущая колода: " + string.Join(", ",
                    info.CurrentDeck.Select(c => $"{c.Name} ур.{c.Level}/{c.MaxLevel}{(c.EvolutionLevel > 0 ? " (эво)" : "")}")));
            var top = info.Cards.OrderByDescending(c => c.Level).Take(12)
                .Select(c => $"{c.Name} {c.Level}");
            sb.AppendLine("Самые прокачанные карты: " + string.Join(", ", top) + ".");
        });

        await Part(sb, async () =>
        {
            try { await collect.SyncAsync(player.PlayerTag, ct); }
            catch { /* журнал не обновился - разбираем то, что уже есть */ }
            var list = await battles.GetSinceAsync(player.PlayerTag, DateTime.UtcNow.AddDays(-30), ct);
            var counted = list.Where(SendMorningDigestUseCase.Counts).OrderBy(b => b.BattleTimeUtc).ToList();
            if (counted.Count == 0) { sb.AppendLine("Боёв в ладдере и Пути легенд за 30 дней нет."); return; }

            var wins = counted.Count(b => b.Result > 0);
            var losses = counted.Count(b => b.Result < 0);
            sb.AppendLine($"За 30 дней: {counted.Count} боёв, {wins} побед, {losses} поражений ({100 * wins / Math.Max(1, counted.Count)}% побед).");

            // Свои колоды: чем играет и с каким успехом
            var decks = counted.Where(b => !string.IsNullOrEmpty(b.DeckKey))
                .GroupBy(b => b.DeckKey).OrderByDescending(g => g.Count()).Take(3);
            foreach (var g in decks)
                sb.AppendLine($"Колода ({g.Count()} боёв, {100 * g.Count(b => b.Result > 0) / g.Count()}% побед): {DeckNames(g.Key, names)}");

            // Неудобные карты соперников: где проигрывает чаще, чем обычно
            var vs = new Dictionary<int, (int Games, int Losses)>();
            foreach (var b in counted.Where(b => !string.IsNullOrEmpty(b.OppDeckKey)))
                foreach (var k in MetaCard.ParseDeckKey(b.OppDeckKey!).Distinct())
                {
                    var cur = vs.GetValueOrDefault(k);
                    vs[k] = (cur.Games + 1, cur.Losses + (b.Result < 0 ? 1 : 0));
                }
            var tough = vs.Where(kv => kv.Value.Games >= 4)
                .Select(kv => (Key: kv.Key, Rate: (double)kv.Value.Losses / kv.Value.Games, kv.Value.Games))
                .Where(x => x.Rate >= 0.55)
                .OrderByDescending(x => x.Rate).Take(5)
                .Select(x => $"{CardName(x.Key, names)} ({(int)(x.Rate * 100)}% поражений в {x.Games} боях)");
            var toughLine = string.Join(", ", tough);
            if (toughLine.Length > 0) sb.AppendLine("Неудобные карты соперников: " + toughLine + ".");

            // Последние поражения с причинами
            var recent = counted.Where(b => b.Result < 0).TakeLast(8).Reverse().ToList();
            if (recent.Count > 0)
            {
                sb.AppendLine("Последние поражения:");
                foreach (var b in recent)
                {
                    var history = counted.Where(x => x.BattleTimeUtc < b.BattleTimeUtc).ToList();
                    var reason = LossReasons.Classify(b, history, null);
                    var why = reason is null ? "" : $"; причина: {ReasonText.GetValueOrDefault(reason.Main, reason.Main)}";
                    var gap = b.LevelGap is double lg ? $"; разница уровней {lg:+0.0;-0.0;0}" : "";
                    sb.AppendLine($"- {b.CrownsFor}:{b.CrownsAgainst} против {(string.IsNullOrEmpty(b.OppDeckKey) ? "?" : DeckNames(b.OppDeckKey!, names))}{gap}{why}");
                }
            }
            var reasons = counted.Where(b => b.Result < 0)
                .Select(b => LossReasons.Classify(b, counted.Where(x => x.BattleTimeUtc < b.BattleTimeUtc).ToList(), null))
                .Where(r => r is not null)
                .GroupBy(r => r!.Main).OrderByDescending(g => g.Count())
                .Select(g => $"{ReasonText.GetValueOrDefault(g.Key, g.Key)} - {g.Count()}");
            var reasonsLine = string.Join("; ", reasons);
            if (reasonsLine.Length > 0) sb.AppendLine("Причины всех поражений за 30 дней: " + reasonsLine + ".");
        });

        sb.Append(await MetaDataAsync(ct));
        return sb.ToString();
    }

    /// <summary>Мета топ-1000: что популярно и что растёт за неделю.</summary>
    private async Task<string> MetaDataAsync(CancellationToken ct)
    {
        try
        {
            var m = await meta.ExecuteAsync(ct);
            if (m is null || m.Cards.Count == 0) return "";
            var sb = new StringBuilder("Мета топ-1000 (доля колод): ");
            sb.AppendLine(string.Join(", ", m.Cards.Take(12).Select(c =>
                $"{c.Name} {c.Percent}%{(c.DeltaPercent is double d && Math.Abs(d) >= 0.5 ? $" ({(d > 0 ? "+" : "")}{d})" : "")}")) + ".");
            return sb.ToString();
        }
        catch (OperationCanceledException) { throw; }
        catch { return ""; }
    }

    private static async Task Part(StringBuilder sb, Func<Task> build)
    {
        try { await build(); }
        catch (OperationCanceledException) { throw; }
        catch { /* кусок не собрался - ответ будет без него */ }
    }

    private async Task<Dictionary<int, string>> CardNamesAsync(CancellationToken ct)
    {
        try
        {
            return (await crApi.GetAllCardsAsync(ct)).Values
                .GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name);
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    /// <summary>Ключ карты колоды → имя. Эволюция в ключе - отрицательный id.</summary>
    private static string CardName(int key, Dictionary<int, string> names) =>
        (names.GetValueOrDefault(Math.Abs(key)) ?? $"#{Math.Abs(key)}") + (key < 0 ? " (эво)" : "");

    private static string DeckNames(string deckKey, Dictionary<int, string> names) =>
        string.Join(", ", MetaCard.ParseDeckKey(deckKey).Select(k => CardName(k, names)));
}
