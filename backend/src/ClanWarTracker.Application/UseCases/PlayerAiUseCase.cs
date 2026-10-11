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

    /// <param name="Left">Сколько ещё осталось сегодня.</param>
    /// <param name="Plus">У человека Плюс - для подсказки «с Плюсом больше».</param>
    public record Answer(Status Status, string? Text, int Left, int Limit, bool Plus, int PlusLimit);

    public static string Today(DateTime nowUtc) => nowUtc.AddMinutes(TzOffsetMinutes).ToString("yyyy-MM-dd");

    /* ---------------- Функции ---------------- */

    /// <summary>Разбор последних поражений: что повторяется и что с этим делать.</summary>
    public Task<Answer> AnalyzeLossesAsync(long tg, string? lang, CancellationToken ct = default) =>
        RunAsync(tg, Analysis, lang, needPlayer: true, async (player, l) =>
        {
            var data = await PlayerDataAsync(player!, ct);
            return Prompt(l, """
                Ты опытный тренер по Clash Royale. Ниже данные игрока: профиль, колода, последние бои и причины поражений.
                Разбери его поражения: найди 2-3 самые частые и исправимые причины и дай по каждой конкретный совет -
                с названиями карт, как разыгрывать и что держать против каких карт соперника. Если причина - уровни карт,
                скажи, какие 1-2 карты качать первыми. Если тильт - честно скажи и предложи правило остановки.
                Пиши дружелюбно и по делу, на «ты», без воды: 600-1000 символов. Пункты начинай с «• ».
                Без markdown (без звёздочек и решёток). Только факты из данных, ничего не выдумывай.
                """, data);
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
            return Prompt(l, $"""
                Ты тренер по Clash Royale в личке Telegram-бота Clanify. Игрок пишет тебе сообщение.
                Ответь коротко и по делу (до 800 символов), на «ты», опираясь на данные игрока и мету ниже,
                с конкретными картами и розыгрышами. Если вопрос не про Clash Royale - вежливо скажи, что ты тренер по игре,
                и предложи спросить про колоду, контру или его бои. Без markdown (без звёздочек и решёток).
                Ничего не выдумывай: нет данных - так и скажи.

                Сообщение игрока: {q}
                """, data);
        }, ct, telegramLang);

    /// <summary>Разбор колоды из подборки: почему она подходит и как ей играть.</summary>
    public Task<Answer> ExplainDeckAsync(long tg, string deckName, IReadOnlyList<string> cards, string? lang,
        CancellationToken ct = default) =>
        RunAsync(tg, Deck, lang, needPlayer: true, async (player, l) =>
        {
            var data = await PlayerDataAsync(player!, ct);
            var list = string.Join(", ", cards.Take(8));
            return Prompt(l, $"""
                Ты тренер по Clash Royale. Игрок смотрит колоду «{deckName}»: {list}.
                По данным игрока ниже объясни: 1) подходит ли она ему по уровням его карт и стилю (какими колодами он
                уже побеждает); 2) план игры - как давить и как защищаться; 3) 2-3 ключевых розыгрыша с картами;
                4) против каких карт меты быть осторожнее; 5) какую карту прокачать первой, если есть отстающие.
                На «ты», 700-1100 символов, пункты с «• », без markdown (без звёздочек и решёток). Ничего не выдумывай.
                """, data);
        }, ct);

    /* ---------------- Общий ход ---------------- */

    private async Task<Answer> RunAsync(long tg, string feature, string? lang, bool needPlayer,
        Func<Player?, string, Task<string>> buildPrompt, CancellationToken ct, string? fallbackLang = null)
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
        string? text;
        try { text = await ai.ChatAsync(await buildPrompt(player, l), 1200, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { text = null; }

        var used = await usage.CountAsync(tg, feature, day, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            // Нейросеть не ответила - попытку возвращаем: человек за сбой платить не должен
            await usage.RefundAsync(tg, feature, day, ct);
            return new Answer(Status.Failed, null, Math.Max(0, limit - used + 1), limit, isPlus, limits.Plus);
        }
        return new Answer(Status.Ok, Clean(text), Math.Max(0, limit - used), limit, isPlus, limits.Plus);
    }

    /// <summary>Модель нет-нет да вставит markdown - в Telegram и в приложении он виден звёздочками.</summary>
    private static string Clean(string text) =>
        text.Replace("**", "").Replace("__", "").Replace("### ", "").Replace("## ", "").Replace("# ", "").Trim();

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
                sb.AppendLine($"Колода ({g.Count()} боёв, {100 * g.Count(b => b.Result > 0) / g.Count()}% побед): {Deck(g.Key, names)}");

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
                    sb.AppendLine($"- {b.CrownsFor}:{b.CrownsAgainst} против {(string.IsNullOrEmpty(b.OppDeckKey) ? "?" : Deck(b.OppDeckKey!, names))}{gap}{why}");
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

    private static string Deck(string deckKey, Dictionary<int, string> names) =>
        string.Join(", ", MetaCard.ParseDeckKey(deckKey).Select(k => CardName(k, names)));
}
