namespace ClanWarTracker.Application.Notifications;

/// <summary>Приглашение пройти опрос «что добавить» - в личку покупателям и активным.</summary>
public sealed class FeedbackText
{
    public static FeedbackText For(string? wire) => BotText.ParseLang(wire) switch
    {
        BotLang.Uk => Uk,
        BotLang.En => En,
        _ => Ru,
    };

    /// <summary>Тому, кто купил. {0} - дней Плюса за ответ.</summary>
    public required string Buyer { get; init; }

    /// <summary>Активному игроку. {0} - дней Плюса за ответ.</summary>
    public required string Active { get; init; }

    public required string Button { get; init; }

    public static readonly FeedbackText Ru = new()
    {
        Buyer = "Спасибо, что поддержал Clanify 💜\n\nОдин вопрос на 30 секунд: за что купил и что добавить дальше? Ответ читаю лично, и по нему решаю, что делать следующим.\n\nЗа ответ — +{0} дня Плюса.",
        Active = "Ты часто заходишь в Clanify — значит, твоё мнение решает 🙌\n\n30 секунд: чего не хватает в боте? Ответ читаю лично, и по нему решаю, что делать следующим.\n\nЗа ответ — +{0} дня Плюса.",
        Button = "✍️ Ответить",
    };

    public static readonly FeedbackText Uk = new()
    {
        Buyer = "Дякую, що підтримав Clanify 💜\n\nОдне питання на 30 секунд: за що купив і що додати далі? Відповідь читаю особисто й за нею вирішую, що робити далі.\n\nЗа відповідь — +{0} дні Плюса.",
        Active = "Ти часто заходиш у Clanify — отже, твоя думка вирішує 🙌\n\n30 секунд: чого бракує в боті? Відповідь читаю особисто й за нею вирішую, що робити далі.\n\nЗа відповідь — +{0} дні Плюса.",
        Button = "✍️ Відповісти",
    };

    public static readonly FeedbackText En = new()
    {
        Buyer = "Thanks for supporting Clanify 💜\n\nOne 30-second question: why did you buy and what should we add next? I read every answer myself and use them to decide what to build.\n\nAnswer and get +{0} days of Plus.",
        Active = "You use Clanify a lot, so your opinion matters 🙌\n\n30 seconds: what's missing in the bot? I read every answer myself and use them to decide what to build.\n\nAnswer and get +{0} days of Plus.",
        Button = "✍️ Answer",
    };
}
