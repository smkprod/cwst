using ClanWarTracker.Application.UseCases;

namespace ClanWarTracker.Application.Notifications;

/// <summary>Задания дня в личке: вечернее «серия сгорит» - тем, кто сам ведёт серию.</summary>
public sealed class QuestText
{
    public static QuestText For(string? wire) => BotText.ParseLang(wire) switch
    {
        BotLang.Uk => Uk,
        BotLang.En => En,
        _ => Ru,
    };

    /// <summary>{0} - серия в днях.</summary>
    public required string RemindHead { get; init; }

    public required string RemindTail { get; init; }

    public required string Button { get; init; }

    /// <summary>Заголовок заданий в утреннем дайджесте.</summary>
    public required string DigestHead { get; init; }

    /// <summary>То же, когда серия есть: {0} - серия в днях.</summary>
    public required string DigestHeadStreak { get; init; }

    /// <summary>Задание по ключу; {0} - сколько нужно.</summary>
    public required IReadOnlyDictionary<string, string> Labels { get; init; }

    public string Label(string key, int target) =>
        Labels.TryGetValue(key, out var s) ? string.Format(s, target) : key;

    public static readonly QuestText Ru = new()
    {
        RemindHead = "🔥 Серия заданий — {0} дн. подряд. Если не закрыть сегодняшние, в полночь она сгорит. Осталось:",
        RemindTail = "Напоминание выключается в карточке заданий.",
        Button = "🎯 Задания дня",
        DigestHead = "🎯 Задания на сегодня — закрой все три и начни серию:",
        DigestHeadStreak = "🎯 Задания на сегодня · 🔥 серия {0} дн., не дай ей сгореть:",
        Labels = new Dictionary<string, string>
        {
            [DailyQuestsUseCase.War] = "Сыграй {0} боя в КВ (дуэль — один бой)",
            [DailyQuestsUseCase.Win] = "Выиграй {0} боя в ладдере или на Пути легенд",
            [DailyQuestsUseCase.Play] = "Сыграй {0} боёв в любом режиме",
            [DailyQuestsUseCase.Crowns] = "Победи с тремя коронами",
            [DailyQuestsUseCase.Flawless] = "Победи, не отдав ни одной башни",
            [DailyQuestsUseCase.Streak] = "Выиграй {0} боя подряд",
            [DailyQuestsUseCase.Trainer] = "Реши задачу в тренажёре",
            [DailyQuestsUseCase.Tip] = "Посмотри фишку в тренажёре",
            [DailyQuestsUseCase.Decks] = "Подбери колоду из своих карт",
        },
    };

    public static readonly QuestText Uk = new()
    {
        RemindHead = "🔥 Серія завдань — {0} дн. поспіль. Якщо не закрити сьогоднішні, опівночі вона згорить. Залишилось:",
        RemindTail = "Нагадування вимикається в картці завдань.",
        Button = "🎯 Завдання дня",
        DigestHead = "🎯 Завдання на сьогодні — закрий усі три й почни серію:",
        DigestHeadStreak = "🎯 Завдання на сьогодні · 🔥 серія {0} дн., не дай їй згоріти:",
        Labels = new Dictionary<string, string>
        {
            [DailyQuestsUseCase.War] = "Зіграй {0} бої в КВ (дуель — один бій)",
            [DailyQuestsUseCase.Win] = "Виграй {0} бої в ладері або на Шляху легенд",
            [DailyQuestsUseCase.Play] = "Зіграй {0} боїв у будь-якому режимі",
            [DailyQuestsUseCase.Crowns] = "Переможи з трьома коронами",
            [DailyQuestsUseCase.Flawless] = "Переможи, не віддавши жодної вежі",
            [DailyQuestsUseCase.Streak] = "Виграй {0} бої поспіль",
            [DailyQuestsUseCase.Trainer] = "Розв'яжи задачу в тренажері",
            [DailyQuestsUseCase.Tip] = "Подивись фішку в тренажері",
            [DailyQuestsUseCase.Decks] = "Підбери колоду зі своїх карт",
        },
    };

    public static readonly QuestText En = new()
    {
        RemindHead = "🔥 Quest streak — {0} days in a row. Finish today's quests or it burns at midnight. Left:",
        RemindTail = "You can turn this reminder off in the quests card.",
        Button = "🎯 Daily quests",
        DigestHead = "🎯 Today's quests — finish all three to start a streak:",
        DigestHeadStreak = "🎯 Today's quests · 🔥 {0}-day streak, don't let it burn:",
        Labels = new Dictionary<string, string>
        {
            [DailyQuestsUseCase.War] = "Play {0} clan war battles (a duel counts as one)",
            [DailyQuestsUseCase.Win] = "Win {0} battles in ladder or Path of Legends",
            [DailyQuestsUseCase.Play] = "Play {0} battles in any mode",
            [DailyQuestsUseCase.Crowns] = "Win with three crowns",
            [DailyQuestsUseCase.Flawless] = "Win without losing a tower",
            [DailyQuestsUseCase.Streak] = "Win {0} battles in a row",
            [DailyQuestsUseCase.Trainer] = "Solve a trainer puzzle",
            [DailyQuestsUseCase.Tip] = "Watch a trick in the trainer",
            [DailyQuestsUseCase.Decks] = "Build a deck from your cards",
        },
    };
}
