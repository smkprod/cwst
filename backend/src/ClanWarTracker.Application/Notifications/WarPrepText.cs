namespace ClanWarTracker.Application.Notifications;

/// <summary>«Военный штаб»: пост «проверка перед КВ» в чат клана и колоды каждому в личку.</summary>
public sealed class WarPrepText
{
    public static WarPrepText For(string? wire) => BotText.ParseLang(wire) switch
    {
        BotLang.Uk => Uk,
        BotLang.En => En,
        _ => Ru,
    };

    /// <summary>{0} - клан.</summary>
    public required string PostHead { get; init; }
    /// <summary>{0} - готовы, {1} - из скольких проверено.</summary>
    public required string PostReady { get; init; }
    /// <summary>{0} - сколько могут собрать колоды сильнее.</summary>
    public required string PostBetter { get; init; }
    /// <summary>{0} - сколько не готовы.</summary>
    public required string PostWeak { get; init; }
    /// <summary>{0} - карты через запятую.</summary>
    public required string PostWeakCards { get; init; }
    public required string PostTail { get; init; }

    /// <summary>{0} - клан.</summary>
    public required string DmHead { get; init; }
    /// <summary>{0} - средний уровень того, чем играл, {1} - лучших колод.</summary>
    public required string DmBetterLevel { get; init; }
    public required string DmBetterLosing { get; init; }
    public required string DmWeak { get; init; }
    public required string DmReady { get; init; }
    /// <summary>{0} - карты через запятую.</summary>
    public required string DmUpgrade { get; init; }

    /// <summary>{0} - номер колоды.</summary>
    public required string DeckButton { get; init; }
    public required string OpenButton { get; init; }

    public static readonly WarPrepText Ru = new()
    {
        PostHead = "🛡 Проверка перед КВ — {0}",
        PostReady = "✅ Готовы: {0} из {1}",
        PostBetter = "🔧 Могут собрать колоды сильнее: {0}",
        PostWeak = "⚠️ Не хватает карт или уровней: {0}",
        PostWeakCards = "Слабое место клана: {0} — их стоит качать.",
        PostTail = "Штаб подобрал каждому 4 колоды на войну из его же карт, без повторов. Жми кнопку — и скопируй их в игру до четверга.",
        DmHead = "🛡 Штаб клана {0}: твои колоды на КВ",
        DmBetterLevel = "Ты воюешь колодами со средним уровнем {0}, а из твоих карт собираются четыре колоды {1}. Это заметная разница в каждом бою.",
        DmBetterLosing = "На прошлой войне твои колоды чаще проигрывали. Попробуй эти — из твоих же карт, по мете топа:",
        DmWeak = "На четыре сильные колоды пока не хватает карт или уровней. Вот лучшее, что собирается сейчас:",
        DmReady = "Ты готов. Вот четыре лучшие колоды из твоих карт — на случай, если захочешь сверить:",
        DmUpgrade = "Что качать в первую очередь: {0}.",
        DeckButton = "⚔️ Колода {0}",
        OpenButton = "🛡 Мои колоды на КВ",
    };

    public static readonly WarPrepText Uk = new()
    {
        PostHead = "🛡 Перевірка перед КВ — {0}",
        PostReady = "✅ Готові: {0} з {1}",
        PostBetter = "🔧 Можуть зібрати колоди сильніші: {0}",
        PostWeak = "⚠️ Бракує карт або рівнів: {0}",
        PostWeakCards = "Слабке місце клану: {0} — їх варто качати.",
        PostTail = "Штаб підібрав кожному 4 колоди на війну з його ж карт, без повторів. Тисни кнопку — і скопіюй їх у гру до четверга.",
        DmHead = "🛡 Штаб клану {0}: твої колоди на КВ",
        DmBetterLevel = "Ти воюєш колодами із середнім рівнем {0}, а з твоїх карт збираються чотири колоди {1}. Це помітна різниця в кожному бою.",
        DmBetterLosing = "На минулій війні твої колоди частіше програвали. Спробуй ці — з твоїх же карт, за метою топу:",
        DmWeak = "На чотири сильні колоди поки бракує карт або рівнів. Ось найкраще, що збирається зараз:",
        DmReady = "Ти готовий. Ось чотири найкращі колоди з твоїх карт — якщо захочеш звірити:",
        DmUpgrade = "Що качати насамперед: {0}.",
        DeckButton = "⚔️ Колода {0}",
        OpenButton = "🛡 Мої колоди на КВ",
    };

    public static readonly WarPrepText En = new()
    {
        PostHead = "🛡 Pre-war check — {0}",
        PostReady = "✅ Ready: {0} of {1}",
        PostBetter = "🔧 Can build stronger decks: {0}",
        PostWeak = "⚠️ Missing cards or levels: {0}",
        PostWeakCards = "Clan weak spot: {0} — worth upgrading.",
        PostTail = "HQ picked 4 war decks for everyone from their own cards, no repeats. Tap the button and copy them into the game before Thursday.",
        DmHead = "🛡 {0} HQ: your war decks",
        DmBetterLevel = "You fight with decks averaging level {0}, but your cards make four decks at {1}. That's a real edge in every battle.",
        DmBetterLosing = "Your decks lost more often last war. Try these — from your own cards, by the top-ladder meta:",
        DmWeak = "You don't have the cards or levels for four strong decks yet. Here's the best you can build now:",
        DmReady = "You're ready. Here are the four best decks from your cards, if you want to compare:",
        DmUpgrade = "Upgrade first: {0}.",
        DeckButton = "⚔️ Deck {0}",
        OpenButton = "🛡 My war decks",
    };
}
