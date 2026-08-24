namespace ClanWarTracker.Application.Notifications;

/// <summary>Язык, на котором бот пишет сообщения клану.</summary>
public enum BotLang { Ru = 0, Uk = 1, En = 2 }

/// <summary>
/// Тексты сообщений бота на трёх языках.
///
/// Свойства объявлены как <c>required</c> намеренно: забыть перевод нельзя физически —
/// новый ключ без украинского или английского варианта просто не скомпилируется.
/// Раньше все формулировки были вшиты в места отправки по-русски, и клан, играющий
/// на английском, получал напоминания, которых не понимал.
///
/// Строки с <c>{0}</c> — шаблоны для <see cref="string.Format(string, object?[])"/>.
/// Числа форматируются на месте вызова и приходят сюда уже строками: так порядок
/// разрядов не зависит от культуры потока, в котором работает воркер.
/// </summary>
public sealed class BotText
{
    public static BotLang ParseLang(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        "uk" => BotLang.Uk,
        "en" => BotLang.En,
        _ => BotLang.Ru,
    };

    public static string ToWire(BotLang lang) => lang switch
    {
        BotLang.Uk => "uk",
        BotLang.En => "en",
        _ => "ru",
    };

    public static BotText For(BotLang lang) => lang switch
    {
        BotLang.Uk => Uk,
        BotLang.En => En,
        _ => Ru,
    };

    public static BotText For(string? wire) => For(ParseLang(wire));

    /* --- Начало войны --- */
    public required string WarStartTitle { get; init; }
    public required string ColosseumStartTitle { get; init; }
    public required string WarStartChat { get; init; }
    /// <summary>{0} — название клана.</summary>
    public required string WarStartDm { get; init; }

    /* --- Напоминания и «пинок» --- */
    /// <summary>{0} — осталось колод, {1} — часов, {2} — минут.</summary>
    public required string ReminderDm { get; init; }
    /// <summary>{0} — осталось колод, {1} — часов, {2} — минут.</summary>
    public required string NudgeDm { get; init; }
    public required string ReminderChatTitle { get; init; }
    public required string NudgeChatTitle { get; init; }
    /// <summary>{0} — упоминание игрока, {1} — сколько колод осталось.</summary>
    public required string SlackerRow { get; init; }
    /// <summary>{0} — сколько игроков без Telegram.</summary>
    public required string ReminderUnlinked { get; init; }
    /// <summary>{0} — сколько игроков без Telegram.</summary>
    public required string NudgeUnlinked { get; init; }
    /// <summary>{0} — сколько игроков без Telegram.</summary>
    public required string FinalCallUnlinked { get; init; }
    public required string ProUpsell { get; init; }
    public required string FinalCallTitle { get; init; }

    /* --- Отчёт за день и итог недели --- */
    /// <summary>{0} — номер дня войны (1..4).</summary>
    public required string DayDone { get; init; }
    /// <summary>{0} — медали за день.</summary>
    public required string DayMedals { get; init; }
    public required string TopOfDay { get; init; }
    public required string NotFinishedTitle { get; init; }
    /// <summary>{0} — имя/упоминание, {1} — сыграно колод.</summary>
    public required string DaySlackerRow { get; init; }
    /// <summary>{0} — сколько ещё не поместилось в список.</summary>
    public required string AndMore { get; init; }
    public required string PerfectDayAll { get; init; }
    public required string FooterDay { get; init; }
    public required string WeekDoneWar { get; init; }
    public required string WeekDoneColosseum { get; init; }
    /// <summary>{0} — медали за неделю, {1} — сколько человек участвовало.</summary>
    public required string WeekMedals { get; init; }
    /// <summary>{0} — имя, {1} — медали.</summary>
    public required string WeekMvp { get; init; }
    public required string TopOfWeek { get; init; }
    public required string FooterWeek { get; init; }

    /* --- Идеальный день: 900 медалей за день ({0} — имя игрока) --- */
    public required string[] PerfectDayJokes { get; init; }

    /* --- Респекты --- */
    public required string RespectTitle { get; init; }
    /// <summary>{0} — сколько респектов роздано за сегодня.</summary>
    public required string RespectFooter { get; init; }

    /* --- Персональный алерт о влиянии на победу --- */
    /// <summary>{0} — шанс сейчас, {1} — шанс без твоих атак, {2} — осталось колод.</summary>
    public required string SmartAlert { get; init; }

    /* --- Истечение Pro --- */
    /// <summary>{0} — дата окончания.</summary>
    public required string PlanSevenDays { get; init; }
    /// <summary>{0} — дата окончания.</summary>
    public required string PlanThreeDays { get; init; }
    public required string PlanExpired { get; init; }

    /* --- Приглашение друга --- */
    /// <summary>{0} — имя пришедшего игрока.</summary>
    public required string ReferralJoined { get; init; }

    /* --- Утренний брифинг лидера --- */
    /// <summary>{0} — «Война»/«Колизей», {1} — номер дня.</summary>
    public required string BriefTitle { get; init; }
    public required string BriefWar { get; init; }
    public required string BriefColosseum { get; init; }
    /// <summary>{0} — очки, {1} — место дня.</summary>
    public required string BriefYesterday { get; init; }
    /// <summary>{0} — место, {1} — всего кланов, {2} — медали.</summary>
    public required string BriefRace { get; init; }
    /// <summary>{0} — имя лидера гонки, {1} — отставание.</summary>
    public required string BriefBehindLeader { get; init; }
    /// <summary>{0} — имя второго клана, {1} — отрыв.</summary>
    public required string BriefAheadSecond { get; init; }
    /// <summary>{0} — итог прошлой недели, {1} — место.</summary>
    public required string BriefVsLastWeek { get; init; }
    /// <summary>{0} — насколько опережаете.</summary>
    public required string BriefAheadOfPace { get; init; }
    /// <summary>{0} — насколько отстаёте.</summary>
    public required string BriefBehindPace { get; init; }
    public required string BriefAlreadyBeaten { get; init; }
    /// <summary>{0} — сколько нужно в день, {1} — текущий темп.</summary>
    public required string BriefNeedPerDay { get; init; }
    /// <summary>{0} — сколько недель, {1} — спарклайн, {2} — тренд.</summary>
    public required string BriefForm { get; init; }
    public required string BriefTrendUp { get; init; }
    public required string BriefTrendDown { get; init; }
    public required string BriefTrendFlat { get; init; }
    /// <summary>{0} — начало периода, {1} — конец периода.</summary>
    public required string BriefFormRange { get; init; }
    public required string BriefAllPlayed { get; init; }
    /// <summary>{0} — сколько не доиграли, {1} — размер состава.</summary>
    public required string BriefSlackers { get; init; }
    /// <summary>{0} — имя, {1} — сыграно колод.</summary>
    public required string BriefSlackerRow { get; init; }
    public required string BriefNudgeHint { get; init; }

    public static readonly BotText Ru = new()
    {
        WarStartTitle = "⚔️ Клановая война началась!",
        ColosseumStartTitle = "🏟 Колизей начался!",
        WarStartChat = "Пора отыграть 4/4 колоды — не подведи клан! 💪",
        WarStartDm = "Зайди и отыграй 4/4 колоды за клан {0}. Удачи! 🍀",

        ReminderDm = "⚔️ Ты ещё не сыграл Clan War!\nОсталось колод: {0}/4\nДо конца дня войны: ~{1} ч {2} мин",
        NudgeDm = "👊 Пинок тебе под зад го кв\nОсталось колод: {0}/4\nДо конца дня: ~{1} ч {2} мин",
        ReminderChatTitle = "⏰ <b>Ещё не доиграли войну:</b>",
        NudgeChatTitle = "👊 <b>Админ пнул лентяев!</b>\nНужно срочно отыграть Клановую войну:",
        SlackerRow = "• {0} — осталось {1}/4 🃏",
        ReminderUnlinked = "👥 Ещё <b>{0}</b> без Telegram — пусть привяжут аккаунт в боте.",
        NudgeUnlinked = "👥 Ещё <b>{0}</b> без Telegram — их тег не достанет. "
                      + "Админ может привязать их сам: ответь на сообщение игрока командой /bind #ТЕГ",
        FinalCallUnlinked = "👥 Ещё <b>{0}</b> без Telegram — админ может привязать через /bind.",
        ProUpsell = "🔒 Личные напоминания в DM — функция Pro. Подключи Pro, чтобы никто не забывал про атаки.",
        FinalCallTitle = "🚨 <b>Война закрывается через ~30 минут!</b>\nПоследний шанс доиграть КВ:",

        DayDone = "🌙 День {0} войны завершён!",
        DayMedals = "🏅 Медали за день: {0}",
        TopOfDay = "Лучшие за день:",
        NotFinishedTitle = "😴 <b>Не доиграли:</b>",
        DaySlackerRow = "• {0} — {1}/4 🃏",
        AndMore = "…и ещё {0}",
        PerfectDayAll = "💪 Все отыграли 4/4 — идеальный день!",
        FooterDay = "Полная статистика и прогноз — в Mini App 👇",
        WeekDoneWar = "🏁 Война недели завершена!",
        WeekDoneColosseum = "🏁 Колизей завершён!",
        WeekMedals = "🏅 Медалей за неделю: {0} · участвовали {1}",
        WeekMvp = "👑 MVP недели — {0} ({1} медалей)!",
        TopOfWeek = "Топ недели:",
        FooterWeek = "История войн, рейтинг и турниры — в Mini App 👇",

        PerfectDayJokes =
        [
            "🏆 {0} набил 900 за день! Чемпион. Передай остальным, где брал читы 😎",
            "👑 {0} — 900/900 за день! Противники уже пишут жалобу в Supercell 📝",
            "🚀 {0} набрал 900 медалей за день. NASA интересуется его руками 🚀",
            "💪 {0} сделал идеальный день: 900! Ни одной осечки — машина, а не игрок",
            "⚡ 900 за день от {0}! Оставь немного медалей другим, жадина 😄",
            "🔥 {0} закрыл день на 900! Скамейка запасных в шоке, тренер плачет от счастья",
            "🎯 {0} — 900 из 900! Снайпер. В следующий раз пусть играет с закрытыми глазами",
        ],

        RespectTitle = "👏 <b>Респекты дня</b>",
        RespectFooter = "<i>Всего за сегодня: {0}. Респект можно дать раз в день — загляни в приложение.</i>",

        SmartAlert = "📉 Без твоих атак шанс клана на победу упадёт с {0}% до {1}%!\n"
                   + "Осталось колод: {2}/4 — успей сыграть.",

        PlanSevenDays = "⏳ Pro-тариф клана заканчивается примерно через 7 дней ({0} UTC).\n\n"
                      + "Без Pro будут недоступны:\n"
                      + "• Безлимитные личные напоминания (Free — только 5 игроков)\n"
                      + "• Прогноз клана и игроков\n"
                      + "• История войн и DNA-профили\n"
                      + "• Кнопка «Пнуть всех» без ограничений\n\n"
                      + "Свяжитесь с администратором сервиса для продления.",
        PlanThreeDays = "⚠️ Pro-тариф клана заканчивается через ~3 дня ({0} UTC).\n\n"
                      + "Поспешите продлить, чтобы не потерять прогнозы, историю и безлимитные напоминания!",
        PlanExpired = "🔒 Pro-тариф клана истёк — клан переведён на Free.\n\n"
                    + "Напоминания теперь ограничены 5 привязанными игроками; "
                    + "прогнозы, история и DNA-профили недоступны.\n\n"
                    + "Для продления обратитесь к администратору сервиса.",

        ReferralJoined = "🎉 По твоей ссылке в Clanify зашёл новый игрок: {0}. Спасибо, что зовёшь друзей!",

        BriefTitle = "🌅 Брифинг лидера · {0} · день {1}/4",
        BriefWar = "Война",
        BriefColosseum = "Колизей",
        BriefYesterday = "Вчера: {0} 🏅 ({1}-е место дня)",
        BriefRace = "📊 Гонка: {0}-е из {1} · {2} 🏅",
        BriefBehindLeader = "🔴 До 1-го ({0}): {1} 🏅",
        BriefAheadSecond = "🟢 Отрыв от 2-го ({0}): {1} 🏅",
        BriefVsLastWeek = "⚖️ Против прошлой недели (итог {0} 🏅 · {1}-е):",
        BriefAheadOfPace = "📈 Опережаете график на {0} 🏅",
        BriefBehindPace = "📉 Отстаёте от графика на {0} 🏅",
        BriefAlreadyBeaten = "🎉 Прошлая неделя уже побита!",
        BriefNeedPerDay = "🎯 Чтобы побить: {0} 🏅/день (сейчас темп ~{1})",
        BriefForm = "📊 Форма ({0} нед.): {1} {2}",
        BriefTrendUp = "растёте 📈",
        BriefTrendDown = "проседаете 📉",
        BriefTrendFlat = "стабильно ➡️",
        BriefFormRange = "{0} → {1} за неделю",
        BriefAllPlayed = "✅ Все уже отыграли 4/4 — отличный старт дня!",
        BriefSlackers = "🎯 Не доиграли: {0} из {1} — пни их:",
        BriefSlackerRow = "• {0} — {1}/4",
        BriefNudgeHint = "👉 Открой Mini App → кнопка «Пнуть» разошлёт им напоминание.",
    };

    public static readonly BotText Uk = new()
    {
        WarStartTitle = "⚔️ Кланова війна почалася!",
        ColosseumStartTitle = "🏟 Колізей почався!",
        WarStartChat = "Час відіграти 4/4 колоди — не підведи клан! 💪",
        WarStartDm = "Зайди та відіграй 4/4 колоди за клан {0}. Щасти! 🍀",

        ReminderDm = "⚔️ Ти ще не зіграв Clan War!\nЗалишилось колод: {0}/4\nДо кінця дня війни: ~{1} год {2} хв",
        NudgeDm = "👊 Копняк тобі — гайда на КВ\nЗалишилось колод: {0}/4\nДо кінця дня: ~{1} год {2} хв",
        ReminderChatTitle = "⏰ <b>Ще не дограли війну:</b>",
        NudgeChatTitle = "👊 <b>Адмін розштовхав лінивих!</b>\nТреба терміново відіграти Кланову війну:",
        SlackerRow = "• {0} — залишилось {1}/4 🃏",
        ReminderUnlinked = "👥 Ще <b>{0}</b> без Telegram — хай прив’яжуть акаунт у боті.",
        NudgeUnlinked = "👥 Ще <b>{0}</b> без Telegram — тег їх не дістане. "
                      + "Адмін може прив’язати їх сам: дай відповідь на повідомлення гравця командою /bind #ТЕГ",
        FinalCallUnlinked = "👥 Ще <b>{0}</b> без Telegram — адмін може прив’язати через /bind.",
        ProUpsell = "🔒 Особисті нагадування в DM — функція Pro. Підключи Pro, щоб ніхто не забував про атаки.",
        FinalCallTitle = "🚨 <b>Війна зачиняється за ~30 хвилин!</b>\nОстанній шанс дограти КВ:",

        DayDone = "🌙 День {0} війни завершено!",
        DayMedals = "🏅 Медалі за день: {0}",
        TopOfDay = "Найкращі за день:",
        NotFinishedTitle = "😴 <b>Не дограли:</b>",
        DaySlackerRow = "• {0} — {1}/4 🃏",
        AndMore = "…і ще {0}",
        PerfectDayAll = "💪 Усі відіграли 4/4 — ідеальний день!",
        FooterDay = "Повна статистика та прогноз — у Mini App 👇",
        WeekDoneWar = "🏁 Війну тижня завершено!",
        WeekDoneColosseum = "🏁 Колізей завершено!",
        WeekMedals = "🏅 Медалей за тиждень: {0} · брали участь {1}",
        WeekMvp = "👑 MVP тижня — {0} ({1} медалей)!",
        TopOfWeek = "Топ тижня:",
        FooterWeek = "Історія війн, рейтинг і турніри — у Mini App 👇",

        PerfectDayJokes =
        [
            "🏆 {0} набив 900 за день! Чемпіон. Розкажи іншим, де брав чіти 😎",
            "👑 {0} — 900/900 за день! Суперники вже пишуть скаргу в Supercell 📝",
            "🚀 {0} набрав 900 медалей за день. NASA цікавиться його руками 🚀",
            "💪 {0} зробив ідеальний день: 900! Жодної осічки — машина, а не гравець",
            "⚡ 900 за день від {0}! Залиш трохи медалей іншим, жаднюго 😄",
            "🔥 {0} закрив день на 900! Лава запасних у шоці, тренер плаче від щастя",
            "🎯 {0} — 900 із 900! Снайпер. Наступного разу хай грає із заплющеними очима",
        ],

        RespectTitle = "👏 <b>Респекти дня</b>",
        RespectFooter = "<i>Усього за сьогодні: {0}. Респект можна дати раз на день — зазирни в застосунок.</i>",

        SmartAlert = "📉 Без твоїх атак шанс клану на перемогу впаде з {0}% до {1}%!\n"
                   + "Залишилось колод: {2}/4 — устигни зіграти.",

        PlanSevenDays = "⏳ Pro-тариф клану завершується приблизно через 7 днів ({0} UTC).\n\n"
                      + "Без Pro будуть недоступні:\n"
                      + "• Безлімітні особисті нагадування (Free — лише 5 гравців)\n"
                      + "• Прогноз клану та гравців\n"
                      + "• Історія війн і DNA-профілі\n"
                      + "• Кнопка «Розштовхати всіх» без обмежень\n\n"
                      + "Зв’яжіться з адміністратором сервісу для продовження.",
        PlanThreeDays = "⚠️ Pro-тариф клану завершується через ~3 дні ({0} UTC).\n\n"
                      + "Поспішіть продовжити, щоб не втратити прогнози, історію та безлімітні нагадування!",
        PlanExpired = "🔒 Pro-тариф клану вичерпано — клан переведено на Free.\n\n"
                    + "Нагадування тепер обмежені 5 прив’язаними гравцями; "
                    + "прогнози, історія та DNA-профілі недоступні.\n\n"
                    + "Для продовження зверніться до адміністратора сервісу.",

        ReferralJoined = "🎉 За твоїм посиланням у Clanify зайшов новий гравець: {0}. Дякуємо, що кличеш друзів!",

        BriefTitle = "🌅 Брифінг лідера · {0} · день {1}/4",
        BriefWar = "Війна",
        BriefColosseum = "Колізей",
        BriefYesterday = "Учора: {0} 🏅 ({1}-е місце дня)",
        BriefRace = "📊 Гонка: {0}-е з {1} · {2} 🏅",
        BriefBehindLeader = "🔴 До 1-го ({0}): {1} 🏅",
        BriefAheadSecond = "🟢 Відрив від 2-го ({0}): {1} 🏅",
        BriefVsLastWeek = "⚖️ Проти минулого тижня (підсумок {0} 🏅 · {1}-е):",
        BriefAheadOfPace = "📈 Випереджаєте графік на {0} 🏅",
        BriefBehindPace = "📉 Відстаєте від графіка на {0} 🏅",
        BriefAlreadyBeaten = "🎉 Минулий тиждень уже побито!",
        BriefNeedPerDay = "🎯 Щоб побити: {0} 🏅/день (зараз темп ~{1})",
        BriefForm = "📊 Форма ({0} тижн.): {1} {2}",
        BriefTrendUp = "зростаєте 📈",
        BriefTrendDown = "просідаєте 📉",
        BriefTrendFlat = "стабільно ➡️",
        BriefFormRange = "{0} → {1} за тиждень",
        BriefAllPlayed = "✅ Усі вже відіграли 4/4 — чудовий старт дня!",
        BriefSlackers = "🎯 Не дограли: {0} з {1} — розштовхай їх:",
        BriefSlackerRow = "• {0} — {1}/4",
        BriefNudgeHint = "👉 Відкрий Mini App → кнопка «Розштовхати» надішле їм нагадування.",
    };

    public static readonly BotText En = new()
    {
        WarStartTitle = "⚔️ Clan War has started!",
        ColosseumStartTitle = "🏟 Colosseum has started!",
        WarStartChat = "Time to play all 4/4 decks — don't let the clan down! 💪",
        WarStartDm = "Jump in and play your 4/4 decks for {0}. Good luck! 🍀",

        ReminderDm = "⚔️ You haven't played Clan War yet!\nDecks left: {0}/4\nWar day ends in: ~{1}h {2}m",
        NudgeDm = "👊 Consider this a kick — get to Clan War\nDecks left: {0}/4\nDay ends in: ~{1}h {2}m",
        ReminderChatTitle = "⏰ <b>Still haven't finished the war:</b>",
        NudgeChatTitle = "👊 <b>The admin nudged the slackers!</b>\nClan War needs playing right now:",
        SlackerRow = "• {0} — {1}/4 decks left 🃏",
        ReminderUnlinked = "👥 <b>{0}</b> more without Telegram — ask them to link their account in the bot.",
        NudgeUnlinked = "👥 <b>{0}</b> more without Telegram — a tag won't reach them. "
                      + "An admin can link them: reply to the player's message with /bind #TAG",
        FinalCallUnlinked = "👥 <b>{0}</b> more without Telegram — an admin can link them via /bind.",
        ProUpsell = "🔒 Private DM reminders are a Pro feature. Go Pro so nobody forgets their attacks.",
        FinalCallTitle = "🚨 <b>The war closes in ~30 minutes!</b>\nLast chance to finish your attacks:",

        DayDone = "🌙 War day {0} is over!",
        DayMedals = "🏅 Medals today: {0}",
        TopOfDay = "Best of the day:",
        NotFinishedTitle = "😴 <b>Didn't finish:</b>",
        DaySlackerRow = "• {0} — {1}/4 🃏",
        AndMore = "…and {0} more",
        PerfectDayAll = "💪 Everyone played 4/4 — a perfect day!",
        FooterDay = "Full stats and forecast — in the Mini App 👇",
        WeekDoneWar = "🏁 The war week is over!",
        WeekDoneColosseum = "🏁 Colosseum is over!",
        WeekMedals = "🏅 Medals this week: {0} · {1} took part",
        WeekMvp = "👑 MVP of the week — {0} ({1} medals)!",
        TopOfWeek = "Top of the week:",
        FooterWeek = "War history, rating and tournaments — in the Mini App 👇",

        PerfectDayJokes =
        [
            "🏆 {0} scored 900 in a day! Champion. Tell the rest where you got the cheats 😎",
            "👑 {0} — 900/900 in a day! The opponents are already filing a complaint with Supercell 📝",
            "🚀 {0} racked up 900 medals in a day. NASA would like a word about those hands 🚀",
            "💪 {0} had a perfect day: 900! Not a single slip — that's a machine, not a player",
            "⚡ 900 in a day from {0}! Leave some medals for the rest of us 😄",
            "🔥 {0} closed the day at 900! The bench is stunned, the coach is crying with joy",
            "🎯 {0} — 900 out of 900! Sniper. Next time try it with your eyes closed",
        ],

        RespectTitle = "👏 <b>Respects of the day</b>",
        RespectFooter = "<i>Today's total: {0}. One respect per day — open the app to give yours.</i>",

        SmartAlert = "📉 Without your attacks the clan's win chance drops from {0}% to {1}%!\n"
                   + "Decks left: {2}/4 — get them in.",

        PlanSevenDays = "⏳ The clan's Pro plan ends in about 7 days ({0} UTC).\n\n"
                      + "Without Pro you lose:\n"
                      + "• Unlimited private reminders (Free — only 5 players)\n"
                      + "• Clan and player forecasts\n"
                      + "• War history and DNA profiles\n"
                      + "• The unlimited \"Nudge everyone\" button\n\n"
                      + "Contact the service admin to renew.",
        PlanThreeDays = "⚠️ The clan's Pro plan ends in ~3 days ({0} UTC).\n\n"
                      + "Renew soon so you don't lose forecasts, history and unlimited reminders!",
        PlanExpired = "🔒 The clan's Pro plan has expired — the clan is back on Free.\n\n"
                    + "Reminders are now limited to 5 linked players; "
                    + "forecasts, history and DNA profiles are unavailable.\n\n"
                    + "Contact the service admin to renew.",

        ReferralJoined = "🎉 A new player joined Clanify through your link: {0}. Thanks for bringing friends!",

        BriefTitle = "🌅 Leader briefing · {0} · day {1}/4",
        BriefWar = "War",
        BriefColosseum = "Colosseum",
        BriefYesterday = "Yesterday: {0} 🏅 (place {1} for the day)",
        BriefRace = "📊 Race: {0} of {1} · {2} 🏅",
        BriefBehindLeader = "🔴 Behind 1st ({0}): {1} 🏅",
        BriefAheadSecond = "🟢 Ahead of 2nd ({0}): {1} 🏅",
        BriefVsLastWeek = "⚖️ Versus last week (finished {0} 🏅 · place {1}):",
        BriefAheadOfPace = "📈 Ahead of pace by {0} 🏅",
        BriefBehindPace = "📉 Behind pace by {0} 🏅",
        BriefAlreadyBeaten = "🎉 Last week is already beaten!",
        BriefNeedPerDay = "🎯 To beat it: {0} 🏅/day (current pace ~{1})",
        BriefForm = "📊 Form ({0} wks): {1} {2}",
        BriefTrendUp = "trending up 📈",
        BriefTrendDown = "sagging 📉",
        BriefTrendFlat = "steady ➡️",
        BriefFormRange = "{0} → {1} over the week",
        BriefAllPlayed = "✅ Everyone already played 4/4 — great start to the day!",
        BriefSlackers = "🎯 Unfinished: {0} of {1} — nudge them:",
        BriefSlackerRow = "• {0} — {1}/4",
        BriefNudgeHint = "👉 Open the Mini App → the \"Nudge\" button sends them a reminder.",
    };
}
