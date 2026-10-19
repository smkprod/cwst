namespace ClanWarTracker.Application.Notifications;

/// <summary>
/// Тексты Хэллоуина: анонс босса в чат клана и канал, «босс повержен». Отдельно от
/// <see cref="BotText"/>: ивент на две недели, и после него класс удаляется целиком.
/// Свойства required - перевод нельзя забыть.
/// </summary>
public sealed class HalloweenText
{
    public static HalloweenText For(string? wire) => BotText.ParseLang(wire) switch
    {
        BotLang.Uk => Uk,
        BotLang.En => En,
        _ => Ru,
    };

    public required string Vampire { get; init; }
    public required string King { get; init; }

    public string Name(string bossKey) => bossKey == "king" ? King : Vampire;

    /// <summary>{0} - день, {1} - месяц в родительном падеже.</summary>
    public required string Date { get; init; }
    public required string[] Months { get; init; }

    /// <summary>
    /// {0} - босс, {1} - здоровье, {2} - до какого дня, {3} - урон за победу в КВ,
    /// {4} - за три короны, {5} - за победу в ладдере, {6} - дней Плюса в награду.
    /// </summary>
    public required string Start { get; init; }

    /// <summary>Строка о прошлом боссе в анонсе нового: {0} - прошлый босс.</summary>
    public required string PrevDefeated { get; init; }

    /// <summary>{0} - прошлый босс, {1} - сколько процентов здоровья у него осталось.</summary>
    public required string PrevEscaped { get; init; }

    /// <summary>{0} - босс, {1} - тройка по урону, {2} - дней Плюса.</summary>
    public required string Killed { get; init; }

    /// <summary>Пост в канал, HTML: {0} - босс, {1} - до какого дня, {2}-{4} - урон, {5} - дней Плюса.</summary>
    public required string Channel { get; init; }

    public required string Button { get; init; }

    public static readonly HalloweenText Ru = new()
    {
        Vampire = "Кровавый вампир",
        King = "Тыквенный король",
        Date = "{0} {1}",
        Months = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"],
        Start = "🎃 Хэллоуин в клане! Пришёл босс — {0}\n\n"
                + "❤️ У него {1} здоровья, и он уйдёт {2}. Бейте его всем кланом:\n"
                + "⚔️ победа в КВ — {3} урона\n"
                + "👑 три короны в КВ — {4}\n"
                + "🏆 победа в ладдере или Пути легенд — {5} (у тех, кто привязал профиль в боте)\n\n"
                + "Добьёте — всем, кто бил, +{6} дня Плюса 💎",
        PrevDefeated = "\n\n🦇 {0} повержен — отличная работа!",
        PrevEscaped = "\n\n🦇 {0} ушёл живым, у него оставалось {1}% здоровья. Этого не упустите!",
        Killed = "💀 {0} повержен!\n\nБольше всех урона:\n{1}\n\nВсем, кто бил и привязал профиль, — +{2} дня Плюса 💎",
        Channel = "🎃 <b>Хэллоуин в Clanify</b>\n\n"
                  + "В каждом клане — босс: <b>{0}</b>. Он уйдёт {1}, и добить его можно только всем кланом:\n"
                  + "⚔️ победа в КВ — {2} урона\n"
                  + "👑 три короны в КВ — {3}\n"
                  + "🏆 победа в ладдере или Пути легенд — {4}\n\n"
                  + "Добьёте — всем, кто бил, +{5} дня Плюса 💎\n\n"
                  + "Здоровье босса и таблица урона — во вкладке «Хэллоуин» в приложении.",
        Button = "🎃 Бить босса",
    };

    public static readonly HalloweenText Uk = new()
    {
        Vampire = "Кривавий вампір",
        King = "Гарбузовий король",
        Date = "{0} {1}",
        Months = ["січня", "лютого", "березня", "квітня", "травня", "червня", "липня", "серпня", "вересня", "жовтня", "листопада", "грудня"],
        Start = "🎃 Хелловін у клані! Прийшов бос — {0}\n\n"
                + "❤️ У нього {1} здоров'я, і він піде {2}. Бийте його всім кланом:\n"
                + "⚔️ перемога у КВ — {3} шкоди\n"
                + "👑 три корони у КВ — {4}\n"
                + "🏆 перемога в ладері чи Шляху легенд — {5} (у тих, хто прив'язав профіль у боті)\n\n"
                + "Доб'єте — всім, хто бив, +{6} дні Плюса 💎",
        PrevDefeated = "\n\n🦇 {0} переможений — чудова робота!",
        PrevEscaped = "\n\n🦇 {0} пішов живим, у нього лишалося {1}% здоров'я. Цього не впустіть!",
        Killed = "💀 {0} переможений!\n\nНайбільше шкоди:\n{1}\n\nУсім, хто бив і прив'язав профіль, — +{2} дні Плюса 💎",
        Channel = "🎃 <b>Хелловін у Clanify</b>\n\n"
                  + "У кожному клані — бос: <b>{0}</b>. Він піде {1}, і добити його можна лише всім кланом:\n"
                  + "⚔️ перемога у КВ — {2} шкоди\n"
                  + "👑 три корони у КВ — {3}\n"
                  + "🏆 перемога в ладері чи Шляху легенд — {4}\n\n"
                  + "Доб'єте — всім, хто бив, +{5} дні Плюса 💎\n\n"
                  + "Здоров'я боса і таблиця шкоди — у вкладці «Хелловін» у застосунку.",
        Button = "🎃 Бити боса",
    };

    public static readonly HalloweenText En = new()
    {
        Vampire = "Blood Vampire",
        King = "Pumpkin King",
        Date = "{1} {0}",
        Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
        Start = "🎃 Halloween in the clan! A boss has arrived — {0}\n\n"
                + "❤️ It has {1} HP and leaves on {2}. Beat it together:\n"
                + "⚔️ clan war win — {3} damage\n"
                + "👑 three crowns in clan war — {4}\n"
                + "🏆 ladder or Path of Legends win — {5} (for players who linked their profile in the bot)\n\n"
                + "Finish it off and everyone who hit it gets +{6} days of Plus 💎",
        PrevDefeated = "\n\n🦇 {0} was defeated — great job!",
        PrevEscaped = "\n\n🦇 {0} got away with {1}% HP left. Don't let this one go!",
        Killed = "💀 {0} defeated!\n\nTop damage:\n{1}\n\nEveryone who hit it and linked their profile gets +{2} days of Plus 💎",
        Channel = "🎃 <b>Halloween in Clanify</b>\n\n"
                  + "Every clan has a boss: <b>{0}</b>. It leaves on {1}, and only the whole clan can finish it:\n"
                  + "⚔️ clan war win — {2} damage\n"
                  + "👑 three crowns in clan war — {3}\n"
                  + "🏆 ladder or Path of Legends win — {4}\n\n"
                  + "Finish it and everyone who hit it gets +{5} days of Plus 💎\n\n"
                  + "Boss HP and the damage table are in the Halloween tab of the app.",
        Button = "🎃 Hit the boss",
    };
}
