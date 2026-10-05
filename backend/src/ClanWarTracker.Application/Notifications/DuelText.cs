namespace ClanWarTracker.Application.Notifications;

/// <summary>
/// Тексты лиги дуэлей. Отдельно от <see cref="BotText"/>: дуэли - самостоятельный
/// раздел со своими карточками, и держать их вместе с напоминаниями о войне незачем.
/// Свойства required по той же причине - перевод нельзя забыть.
/// </summary>
public sealed class DuelText
{
    public static DuelText For(string? wire) => BotText.ParseLang(wire) switch
    {
        BotLang.Uk => Uk,
        BotLang.En => En,
        _ => Ru,
    };

    /// <summary>Лиги по возрастанию: Бронза … Легенда.</summary>
    public required string[] Leagues { get; init; }

    /// <summary>{0} - Bo1/Bo3, {1} - имя, {2} - значок и лига, {3} - рейтинг.</summary>
    public required string Open { get; init; }
    /// <summary>{0} - Bo, {1} - имя, {2} - лига, {3} - рейтинг, {4} - кого вызывают.</summary>
    public required string OpenTargeted { get; init; }
    public required string BtnAccept { get; init; }
    public required string BtnLeague { get; init; }
    public required string BtnJoin { get; init; }
    public required string BtnChallenge { get; init; }

    /// <summary>{0} - Bo, {1} - A, {2} - рейтинг A, {3} - B, {4} - рейтинг B, {5} - счёт A, {6} - счёт B, {7} - минут на всё.</summary>
    public required string Active { get; init; }
    public required string Unrated { get; init; }
    /// <summary>{0} - имя.</summary>
    public required string BtnAddFriend { get; init; }
    public required string BtnRefresh { get; init; }
    public required string BtnCancel { get; init; }

    /// <summary>{0} - победитель, {1} - проигравший, {2} - счёт, {3} - Bo, {4} - строка победителя, {5} - строка проигравшего.</summary>
    public required string Finished { get; init; }
    /// <summary>{0} - имя, {1} - было, {2} - стало, {3} - изменение, {4} - лига.</summary>
    public required string RatingRow { get; init; }
    public required string FinishedUnrated { get; init; }
    /// <summary>{0} - A, {1} - B, {2} - счёт.</summary>
    public required string Expired { get; init; }
    /// <summary>{0} - A, {1} - B.</summary>
    public required string Cancelled { get; init; }
    public required string Footer { get; init; }

    /// <summary>{0} - соперник, {1} - Bo.</summary>
    public required string DmStarted { get; init; }
    /// <summary>{0} - соперник, {1} - счёт, {2} - рейтинг было, {3} - стало, {4} - изменение, {5} - лига.</summary>
    public required string DmWon { get; init; }
    /// <summary>То же для поражения.</summary>
    public required string DmLost { get; init; }

    // Ответы на нажатия
    public required string ToastSelf { get; init; }
    public required string ToastNotTarget { get; init; }
    public required string ToastNeedJoin { get; init; }
    public required string ToastChallengerGone { get; init; }
    public required string ToastYouBusy { get; init; }
    public required string ToastThemBusy { get; init; }
    public required string ToastTaken { get; init; }
    public required string ToastStale { get; init; }
    public required string ToastStarted { get; init; }
    public required string ToastNotYours { get; init; }
    public required string ToastCantCancel { get; init; }
    public required string ToastCancelled { get; init; }
    public required string ToastChecked { get; init; }

    // Личка: вступление и профиль
    public required string NeedLink { get; init; }
    public required string HowToJoin { get; init; }
    /// <summary>{0} - тег из ссылки, {1} - привязанный тег.</summary>
    public required string WrongTag { get; init; }
    public required string BadLink { get; init; }
    /// <summary>{0} - имя, {1} - лига, {2} - рейтинг.</summary>
    public required string Joined { get; init; }
    public required string LinkUpdated { get; init; }
    /// <summary>{0} - имя, {1} - лига, {2} - рейтинг, {3} - место, {4} - побед, {5} - поражений, {6} - пик.</summary>
    public required string Profile { get; init; }
    public required string HowToChallenge { get; init; }
    public required string NeedJoinInGroup { get; init; }
    /// <summary>{0} - « → кому» или пусто.</summary>
    public required string PickFormat { get; init; }

    // Инлайн
    /// <summary>{0} - Bo.</summary>
    public required string InlineTitle { get; init; }
    public required string InlineDesc { get; init; }
    public required string InlineJoinButton { get; init; }

    public string League(int index) => Leagues[Math.Clamp(index, 0, Leagues.Length - 1)];

    public static readonly DuelText Ru = new()
    {
        Leagues = ["Бронза", "Серебро", "Золото", "Алмаз", "Мастер", "Легенда"],
        Open = "⚔️ Вызов на дуэль 1×1 · {0}\n\n{1} · {2} · {3}\n\nКто примет вызов? Счёт бот засчитает сам — по журналу боёв, без скриншотов.",
        OpenTargeted = "⚔️ {1} вызывает {4} на дуэль 1×1 · {0}\n\n{1} · {2} · {3}\n\nСчёт бот засчитает сам — по журналу боёв, без скриншотов.",
        BtnAccept = "✅ Принять вызов",
        BtnLeague = "🏆 Лига",
        BtnJoin = "⚔️ Вступить в лигу",
        BtnChallenge = "⚔️ Вызвать в чате",
        Active = "⚔️ Дуэль · {0}\n\n{1} ({2})  {5} : {6}  {3} ({4})\n\n1. Добавьте друг друга в друзья — кнопки ниже.\n2. Сыграйте дружеский бой: Друзья → ⚔️ Бой.\n3. Бот засчитает счёт сам через минуту после боя.\n\n⏳ На всю дуэль — {7} мин.",
        Unrated = "\n🤝 Товарищеская: эта пара уже сыграла 3 рейтинговые дуэли за сутки.",
        BtnAddFriend = "➕ {0} в друзья",
        BtnRefresh = "🔄 Проверить счёт",
        BtnCancel = "✖ Отменить",
        Finished = "🏆 {0} побеждает {1} · {2} ({3})\n\n{4}\n{5}",
        RatingRow = "{0}: {1} → {2} ({3}) · {4}",
        FinishedUnrated = "🤝 Товарищеская дуэль — рейтинг не менялся.",
        Expired = "⌛ Дуэль {0} — {1} не доиграна ({2}). Рейтинг не изменился.",
        Cancelled = "✖ Дуэль {0} — {1} отменена.",
        Footer = "\n\nВызови и ты: @clanifybot в любом чате → ⚔️",
        DmStarted = "⚔️ Дуэль с {0} началась · {1}\n\nДобавь соперника в друзья кнопкой ниже и сыграй дружеский бой. Счёт засчитаю сам.",
        DmWon = "🏆 Победа над {0} · {1}\nРейтинг: {2} → {3} ({4}) · {5}",
        DmLost = "😤 Поражение от {0} · {1}\nРейтинг: {2} → {3} ({4}) · {5}\n\nРеванш? Вызови снова.",
        ToastSelf = "Это твой вызов — его должен принять соперник.",
        ToastNotTarget = "Этот вызов адресован другому игроку.",
        ToastNeedJoin = "Сначала вступи в лигу дуэлей — это 30 секунд.",
        ToastChallengerGone = "Автор вызова вышел из лиги.",
        ToastYouBusy = "У тебя уже идёт дуэль — доиграй её.",
        ToastThemBusy = "Автор вызова сейчас в другой дуэли.",
        ToastTaken = "Вызов уже принят.",
        ToastStale = "Вызов устарел — попроси бросить новый.",
        ToastStarted = "Дуэль началась! Ссылка в друзья — в личке и под сообщением.",
        ToastNotYours = "Это не твоя дуэль.",
        ToastCantCancel = "Бой уже засчитан — отменить нельзя, доиграйте.",
        ToastCancelled = "Дуэль отменена.",
        ToastChecked = "Проверил журнал боёв.",
        NeedLink = "⚔️ Лига дуэлей Clanify\n\nСначала привяжи свой аккаунт Clash Royale — пришли мне свой тег, например #ABC123.",
        HowToJoin = "⚔️ Лига дуэлей Clanify\n\nВызывай кого угодно на 1×1 прямо в чате, а счёт бот засчитает сам — по журналу боёв. Рейтинг Эло, лиги от Бронзы до Легенды, общий топ.\n\nЧтобы вступить, пришли мне ссылку «добавить в друзья» из игры:\nClash Royale → Друзья (👥) → Пригласить друга → Скопировать ссылку.\n\nПо ней соперник добавит тебя в друзья в один тап.",
        WrongTag = "Это ссылка аккаунта {0}, а у тебя привязан {1}. Пришли ссылку со своего аккаунта.",
        BadLink = "Не похоже на ссылку из игры. Нужна ссылка вида https://link.clashroyale.com/invite/friend/…",
        Joined = "✅ Ты в лиге дуэлей!\n\n{0} · {1} · {2}\n\nКак вызвать: напиши @clanifybot в любом чате и выбери «⚔️ Вызов 1×1», или /duel в чате клана.",
        LinkUpdated = "✅ Ссылка в друзья обновлена.",
        Profile = "⚔️ {0}\n{1} · рейтинг {2} · место #{3}\nПобед {4} · поражений {5} · пик {6}",
        HowToChallenge = "\n\nВызвать: @clanifybot в любом чате → «⚔️ Вызов 1×1», или /duel в чате (ответом на сообщение — вызов конкретному игроку). Новая ссылка в друзья — просто пришли её сюда.",
        NeedJoinInGroup = "⚔️ Чтобы бросать вызовы, вступи в лигу дуэлей — это 30 секунд в личке.",
        PickFormat = "⚔️ Дуэль 1×1{0}: выбери формат.",
        InlineTitle = "⚔️ Вызов на дуэль 1×1 · {0}",
        InlineDesc = "Кто примет — бот сам засчитает счёт и рейтинг",
        InlineJoinButton = "⚔️ Вступить в лигу дуэлей",
    };

    public static readonly DuelText Uk = new()
    {
        Leagues = ["Бронза", "Срібло", "Золото", "Діамант", "Майстер", "Легенда"],
        Open = "⚔️ Виклик на дуель 1×1 · {0}\n\n{1} · {2} · {3}\n\nХто прийме виклик? Рахунок бот зарахує сам — за журналом боїв, без скріншотів.",
        OpenTargeted = "⚔️ {1} викликає {4} на дуель 1×1 · {0}\n\n{1} · {2} · {3}\n\nРахунок бот зарахує сам — за журналом боїв, без скріншотів.",
        BtnAccept = "✅ Прийняти виклик",
        BtnLeague = "🏆 Ліга",
        BtnJoin = "⚔️ Вступити в лігу",
        BtnChallenge = "⚔️ Викликати в чаті",
        Active = "⚔️ Дуель · {0}\n\n{1} ({2})  {5} : {6}  {3} ({4})\n\n1. Додайте одне одного в друзі — кнопки нижче.\n2. Зіграйте дружній бій: Друзі → ⚔️ Бій.\n3. Бот зарахує рахунок сам за хвилину після бою.\n\n⏳ На всю дуель — {7} хв.",
        Unrated = "\n🤝 Товариська: ця пара вже зіграла 3 рейтингові дуелі за добу.",
        BtnAddFriend = "➕ {0} у друзі",
        BtnRefresh = "🔄 Перевірити рахунок",
        BtnCancel = "✖ Скасувати",
        Finished = "🏆 {0} перемагає {1} · {2} ({3})\n\n{4}\n{5}",
        RatingRow = "{0}: {1} → {2} ({3}) · {4}",
        FinishedUnrated = "🤝 Товариська дуель — рейтинг не змінювався.",
        Expired = "⌛ Дуель {0} — {1} не дограна ({2}). Рейтинг не змінився.",
        Cancelled = "✖ Дуель {0} — {1} скасована.",
        Footer = "\n\nВикликай і ти: @clanifybot у будь-якому чаті → ⚔️",
        DmStarted = "⚔️ Дуель з {0} почалася · {1}\n\nДодай суперника в друзі кнопкою нижче і зіграй дружній бій. Рахунок зарахую сам.",
        DmWon = "🏆 Перемога над {0} · {1}\nРейтинг: {2} → {3} ({4}) · {5}",
        DmLost = "😤 Поразка від {0} · {1}\nРейтинг: {2} → {3} ({4}) · {5}\n\nРеванш? Виклич знову.",
        ToastSelf = "Це твій виклик — його має прийняти суперник.",
        ToastNotTarget = "Цей виклик адресований іншому гравцю.",
        ToastNeedJoin = "Спершу вступи в лігу дуелей — це 30 секунд.",
        ToastChallengerGone = "Автор виклику вийшов з ліги.",
        ToastYouBusy = "У тебе вже йде дуель — дограй її.",
        ToastThemBusy = "Автор виклику зараз в іншій дуелі.",
        ToastTaken = "Виклик уже прийнято.",
        ToastStale = "Виклик застарів — попроси кинути новий.",
        ToastStarted = "Дуель почалася! Посилання в друзі — в особистих і під повідомленням.",
        ToastNotYours = "Це не твоя дуель.",
        ToastCantCancel = "Бій уже зараховано — скасувати не можна, догравайте.",
        ToastCancelled = "Дуель скасовано.",
        ToastChecked = "Перевірив журнал боїв.",
        NeedLink = "⚔️ Ліга дуелей Clanify\n\nСпершу прив’яжи свій акаунт Clash Royale — надішли мені свій тег, наприклад #ABC123.",
        HowToJoin = "⚔️ Ліга дуелей Clanify\n\nВикликай будь-кого на 1×1 прямо в чаті, а рахунок бот зарахує сам — за журналом боїв. Рейтинг Ело, ліги від Бронзи до Легенди, спільний топ.\n\nЩоб вступити, надішли мені посилання «додати в друзі» з гри:\nClash Royale → Друзі (👥) → Запросити друга → Скопіювати посилання.\n\nЗа ним суперник додасть тебе в друзі в один тап.",
        WrongTag = "Це посилання акаунта {0}, а в тебе прив’язаний {1}. Надішли посилання зі свого акаунта.",
        BadLink = "Не схоже на посилання з гри. Потрібне посилання виду https://link.clashroyale.com/invite/friend/…",
        Joined = "✅ Ти в лізі дуелей!\n\n{0} · {1} · {2}\n\nЯк викликати: напиши @clanifybot у будь-якому чаті й обери «⚔️ Виклик 1×1», або /duel у чаті клану.",
        LinkUpdated = "✅ Посилання в друзі оновлено.",
        Profile = "⚔️ {0}\n{1} · рейтинг {2} · місце #{3}\nПеремог {4} · поразок {5} · пік {6}",
        HowToChallenge = "\n\nВикликати: @clanifybot у будь-якому чаті → «⚔️ Виклик 1×1», або /duel у чаті (відповіддю на повідомлення — виклик конкретному гравцю). Нове посилання в друзі — просто надішли його сюди.",
        NeedJoinInGroup = "⚔️ Щоб кидати виклики, вступи в лігу дуелей — це 30 секунд в особистих.",
        PickFormat = "⚔️ Дуель 1×1{0}: обери формат.",
        InlineTitle = "⚔️ Виклик на дуель 1×1 · {0}",
        InlineDesc = "Хто прийме — бот сам зарахує рахунок і рейтинг",
        InlineJoinButton = "⚔️ Вступити в лігу дуелей",
    };

    public static readonly DuelText En = new()
    {
        Leagues = ["Bronze", "Silver", "Gold", "Diamond", "Master", "Legend"],
        Open = "⚔️ 1v1 duel challenge · {0}\n\n{1} · {2} · {3}\n\nWho accepts? The bot scores it from the battle log — no screenshots.",
        OpenTargeted = "⚔️ {1} challenges {4} to a 1v1 duel · {0}\n\n{1} · {2} · {3}\n\nThe bot scores it from the battle log — no screenshots.",
        BtnAccept = "✅ Accept",
        BtnLeague = "🏆 League",
        BtnJoin = "⚔️ Join the league",
        BtnChallenge = "⚔️ Challenge in a chat",
        Active = "⚔️ Duel · {0}\n\n{1} ({2})  {5} : {6}  {3} ({4})\n\n1. Add each other as friends — buttons below.\n2. Play a friendly battle: Friends → ⚔️ Battle.\n3. The bot scores it a minute after the battle.\n\n⏳ {7} min for the whole duel.",
        Unrated = "\n🤝 Friendly: this pair already played 3 rated duels today.",
        BtnAddFriend = "➕ Add {0}",
        BtnRefresh = "🔄 Check score",
        BtnCancel = "✖ Cancel",
        Finished = "🏆 {0} beats {1} · {2} ({3})\n\n{4}\n{5}",
        RatingRow = "{0}: {1} → {2} ({3}) · {4}",
        FinishedUnrated = "🤝 Friendly duel — rating unchanged.",
        Expired = "⌛ Duel {0} — {1} wasn't finished ({2}). Rating unchanged.",
        Cancelled = "✖ Duel {0} — {1} cancelled.",
        Footer = "\n\nChallenge someone: @clanifybot in any chat → ⚔️",
        DmStarted = "⚔️ Duel with {0} started · {1}\n\nAdd your opponent with the button below and play a friendly battle. I'll score it myself.",
        DmWon = "🏆 Win vs {0} · {1}\nRating: {2} → {3} ({4}) · {5}",
        DmLost = "😤 Loss vs {0} · {1}\nRating: {2} → {3} ({4}) · {5}\n\nRematch? Challenge again.",
        ToastSelf = "That's your own challenge — an opponent has to accept it.",
        ToastNotTarget = "This challenge is for another player.",
        ToastNeedJoin = "Join the duel league first — it takes 30 seconds.",
        ToastChallengerGone = "The challenger left the league.",
        ToastYouBusy = "You already have a duel in progress — finish it first.",
        ToastThemBusy = "The challenger is in another duel right now.",
        ToastTaken = "Already accepted.",
        ToastStale = "This challenge expired — ask for a new one.",
        ToastStarted = "Duel on! Friend links are in your DMs and under the message.",
        ToastNotYours = "This isn't your duel.",
        ToastCantCancel = "A battle is already scored — finish the duel.",
        ToastCancelled = "Duel cancelled.",
        ToastChecked = "Checked the battle log.",
        NeedLink = "⚔️ Clanify duel league\n\nLink your Clash Royale account first — send me your tag, e.g. #ABC123.",
        HowToJoin = "⚔️ Clanify duel league\n\nChallenge anyone to a 1v1 right in a chat — the bot scores it from the battle log. Elo rating, leagues from Bronze to Legend, a global top.\n\nTo join, send me your in-game friend link:\nClash Royale → Friends (👥) → Invite a friend → Copy link.\n\nOpponents use it to add you in one tap.",
        WrongTag = "That link belongs to {0}, but your linked account is {1}. Send the link from your own account.",
        BadLink = "That doesn't look like a game link. I need one like https://link.clashroyale.com/invite/friend/…",
        Joined = "✅ You're in the duel league!\n\n{0} · {1} · {2}\n\nTo challenge: type @clanifybot in any chat and pick “⚔️ 1v1 duel”, or /duel in your clan chat.",
        LinkUpdated = "✅ Friend link updated.",
        Profile = "⚔️ {0}\n{1} · rating {2} · rank #{3}\nWins {4} · losses {5} · peak {6}",
        HowToChallenge = "\n\nTo challenge: @clanifybot in any chat → “⚔️ 1v1 duel”, or /duel in a chat (as a reply — to challenge that player). New friend link — just send it here.",
        NeedJoinInGroup = "⚔️ Join the duel league to throw challenges — 30 seconds in private chat.",
        PickFormat = "⚔️ 1v1 duel{0}: pick the format.",
        InlineTitle = "⚔️ 1v1 duel challenge · {0}",
        InlineDesc = "Whoever accepts — the bot scores it and updates the rating",
        InlineJoinButton = "⚔️ Join the duel league",
    };
}
