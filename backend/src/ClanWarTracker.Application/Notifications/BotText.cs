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
    /// <summary>
    /// Разбирает код языка. Отрезает región-часть, потому что сюда попадает не только
    /// наша настройка ("uk"), но и язык интерфейса из Telegram — а он приходит как
    /// "en-US" или "uk-UA", и точное сравнение молча роняло бы такого человека в русский.
    /// </summary>
    public static BotLang ParseLang(string? wire)
    {
        var code = wire?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code)) return BotLang.Ru;

        var dash = code.IndexOf('-');
        if (dash > 0) code = code[..dash];

        return code switch
        {
            "uk" => BotLang.Uk,
            "en" => BotLang.En,
            _ => BotLang.Ru,
        };
    }

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

    /* --- Ответы на команды --- */
    public required string StartPrivate { get; init; }
    public required string StartGroupNew { get; init; }
    /// <summary>{0} — название клана.</summary>
    public required string StartGroupReady { get; init; }
    public required string OnlyInGroup { get; init; }
    public required string ClanNotLinked { get; init; }
    public required string SetupFormat { get; init; }
    public required string SetupOnlyAdmin { get; init; }
    public required string SetupClanNotFound { get; init; }
    /// <summary>{0} — название клана.</summary>
    public required string SetupOk { get; init; }
    public required string SetupTopicNote { get; init; }
    public required string LinkFormat { get; init; }
    public required string LinkNotFound { get; init; }
    /// <summary>{0} — имя игрока.</summary>
    public required string LinkOkPrivate { get; init; }
    /// <summary>{0} — имя игрока.</summary>
    public required string LinkOkGroup { get; init; }
    public required string RemindOnlyAdmin { get; init; }
    public required string RemindFormat { get; init; }
    /// <summary>{0} — за сколько часов.</summary>
    public required string RemindOk { get; init; }
    public required string TopicOnlyAdmin { get; init; }
    public required string TopicSetToThread { get; init; }
    public required string TopicSetToChat { get; init; }
    public required string NudgeOnlyAdmin { get; init; }
    public required string NudgeNoWarDay { get; init; }
    public required string NudgeAllPlayed { get; init; }
    /// <summary>{0} — сколько не доиграли и не привязаны.</summary>
    public required string NudgeNobodyTaggable { get; init; }
    public required string BindOnlyAdmin { get; init; }
    public required string BindHelp { get; init; }
    /// <summary>{0} — то, что человек ввёл вместо юзернейма.</summary>
    public required string BindBadUsername { get; init; }
    public required string BindWho { get; init; }
    public required string BindTagNotFound { get; init; }
    public required string BindNotInClan { get; init; }
    /// <summary>{0} — имя игрока, {1} — куда привязан.</summary>
    public required string BindOk { get; init; }
    public required string BindOkAccount { get; init; }
    /// <summary>{0} — прежний тег.</summary>
    public required string BindMoved { get; init; }
    public required string BindNoDm { get; init; }
    /// <summary>Ссылка-приглашение не читается: подделана, испорчена при пересылке или протухла.</summary>
    public required string ClaimBadLink { get; init; }
    /// <summary>Тег уже занят другим аккаунтом — ссылкой воспользовались раньше.</summary>
    public required string ClaimTaken { get; init; }
    /// <summary>{0} — имя игрока.</summary>
    public required string ClaimOk { get; init; }
    public required string UnbindOnlyAdmin { get; init; }
    public required string UnbindNeedTag { get; init; }
    /// <summary>{0} — имя игрока.</summary>
    public required string UnbindOk { get; init; }
    public required string UnbindNothing { get; init; }
    public required string UnlinkedRosterFail { get; init; }
    public required string UnlinkedAllLinked { get; init; }
    /// <summary>{0} — сколько непривязанных, {1} — список.</summary>
    public required string UnlinkedList { get; init; }
    public required string StatusNoWarData { get; init; }
    /// <summary>{0} — клан, {1} — период.</summary>
    public required string StatusHeader { get; init; }
    /// <summary>{0} — сыграли, {1} — всего.</summary>
    public required string StatusPlayed { get; init; }
    /// <summary>{0} — часов до конца дня.</summary>
    public required string StatusHoursLeft { get; init; }
    /// <summary>{0} — прогноз дня, {1} — прогноз недели.</summary>
    public required string StatusForecast { get; init; }
    /// <summary>{0} — сколько игроков не поместилось.</summary>
    public required string StatusMore { get; init; }
    public required string PeriodWarDay { get; init; }
    public required string PeriodColosseum { get; init; }
    public required string PeriodTraining { get; init; }
    public required string ErrCrApiToken { get; init; }
    public required string ErrCrApiDown { get; init; }
    /// <summary>{0} — описание ошибки.</summary>
    public required string ErrDb { get; init; }
    /// <summary>{0} — описание ошибки.</summary>
    public required string ErrGeneric { get; init; }
    /// <summary>{0} — введённый тег.</summary>
    public required string QuickNotFound { get; init; }
    /// <summary>{0} — имя игрока.</summary>
    public required string QuickNoClan { get; init; }
    /// <summary>{0} — имя игрока, {1} — тег клана.</summary>
    public required string QuickNoWarData { get; init; }
    /// <summary>{0} — имя игрока, {1} — название клана.</summary>
    public required string QuickHeader { get; init; }
    /// <summary>{0} — «Война»/«Колизей», {1} — часов до конца дня.</summary>
    public required string QuickWarLine { get; init; }
    /// <summary>{0} — отыграли, {1} — всего.</summary>
    public required string QuickPlayed { get; init; }
    /// <summary>{0} — слава, {1} — место.</summary>
    public required string QuickMeAll { get; init; }
    /// <summary>{0} — слава, {1} — место.</summary>
    public required string QuickMeNone { get; init; }
    /// <summary>{0} — колод сыграно, {1} — слава, {2} — место.</summary>
    public required string QuickMeSome { get; init; }
    public required string QuickLaggardsTitle { get; init; }
    /// <summary>{0} — имя, {1} — колод сыграно.</summary>
    public required string QuickLaggardRow { get; init; }
    /// <summary>{0} — сколько ещё.</summary>
    public required string QuickAndMore { get; init; }
    public required string QuickNotInWar { get; init; }
    public required string QuickTraining { get; init; }
    /// <summary>{0} — сколько участников.</summary>
    public required string QuickMembers { get; init; }
    public required string QuickFooter { get; init; }
    public required string QuickShareText { get; init; }
    public required string QuickShareButton { get; init; }

    /* --- Inline-режим: карточка в любом чате Telegram --- */
    public required string InlineWarTitle { get; init; }
    public required string InlineWarDesc { get; init; }
    /// <summary>{0} — имя, {1} — клан, {2} — медали, {3} — место, {4} — колод сегодня.</summary>
    public required string InlineWarText { get; init; }
    public required string InlineClanTitle { get; init; }
    public required string InlineClanDesc { get; init; }
    /// <summary>{0} — клан, {1} — место в гонке, {2} — всего кланов, {3} — медали, {4} — не отыграли.</summary>
    public required string InlineClanText { get; init; }
    public required string InlineNoLinkTitle { get; init; }
    public required string InlineNoLinkDesc { get; init; }
    public required string InlineNoLinkText { get; init; }
    public required string InlineLinkButton { get; init; }
    public required string InlineOpenBot { get; init; }
    public required string InlineFooter { get; init; }

    /* --- Inline: дополнительные карточки --- */
    public required string InlineProfileTitle { get; init; }
    public required string InlineProfileDesc { get; init; }
    /// <summary>{0} — имя, {1} — уровень, {2} — кубки, {3} — рекорд, {4} — победы в КВ, {5} — «три короны».</summary>
    public required string InlineProfileText { get; init; }
    public required string InlineDeckTitle { get; init; }
    public required string InlineDeckDesc { get; init; }
    /// <summary>{0} — имя, {1} — карты через точку, {2} — средний уровень.</summary>
    public required string InlineDeckText { get; init; }
    public required string InlineDeckOpen { get; init; }
    public required string InlineTopTitle { get; init; }
    public required string InlineTopDesc { get; init; }
    /// <summary>{0} — клан, {1} — строки топ-3.</summary>
    public required string InlineTopText { get; init; }
    public required string InlineLastWarTitle { get; init; }
    public required string InlineLastWarDesc { get; init; }
    /// <summary>{0} — клан, {1} — место, {2} — медали, {3} — изменение трофеев со знаком.</summary>
    public required string InlineLastWarText { get; init; }

    /* --- Inline: поиск по тегу, клан, колоды топа --- */
    public required string InlineFoundTitle { get; init; }
    public required string InlineFoundDesc { get; init; }
    public required string InlineNotFoundTitle { get; init; }
    public required string InlineNotFoundDesc { get; init; }
    /// <summary>{0} — введённый тег.</summary>
    public required string InlineNotFoundText { get; init; }
    public required string InlineClanCardTitle { get; init; }
    public required string InlineClanCardDesc { get; init; }
    /// <summary>{0} — клан, {1} — тег, {2} — участников, {3} — очки, {4} — КВ-трофеи, {5} — порог кубков.</summary>
    public required string InlineClanCardText { get; init; }
    public required string InlineTopDecksTitle { get; init; }
    public required string InlineTopDecksDesc { get; init; }
    /// <summary>{0} — сколько игроков в выборке, {1} — строки с картами.</summary>
    public required string InlineTopDecksText { get; init; }
    public required string InlineTopDeckOne { get; init; }

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

        StartPrivate = "⚔️ Clanify — статистика войны Clash Royale\n\n"
                     + "Отправь свой тег аккаунта CR прямо сюда — например:\n"
                     + "#2VUPLPU0R\n\n"
                     + "Я сразу покажу:\n"
                     + "• кто не атакует в войне твоего клана\n"
                     + "• твой личный счёт и место в рейтинге\n"
                     + "• сколько часов осталось до конца дня\n\n"
                     + "Работает для всех участников — не только лидеров.\n\n"
                     + "Или открой Mini App кнопкой в меню ниже 👇",
        StartGroupNew = "⚔️ Clanify — статистика войны Clash Royale\n\n"
                      + "Чтобы подключить клан к этой группе, лидер или администратор выполняет:\n"
                      + "/setup #ТЕГ_КЛАНА\n\n"
                      + "После этого каждый участник может написать боту /start в ЛС и отправить свой тег — и сразу увидит статистику.",
        StartGroupReady = "⚔️ Клан «{0}» подключён!\n"
                        + "/status — статус текущей войны\n"
                        + "/remind N — напоминания за N часов до конца дня\n"
                        + "/nudge — пнуть тех, кто не отыграл (тег по @username)\n"
                        + "/bind #ТЕГ — привязать игрока к Telegram (ответом на его сообщение)\n"
                        + "/unlinked — кого ещё не привязали\n"
                        + "/settopic — слать уведомления в эту тему (запусти внутри темы)\n\n"
                        + "Участники: напишите боту /start в личку и отправьте свой тег CR.",
        OnlyInGroup = "⚠️ Команда работает только в групповом чате клана.",
        ClanNotLinked = "Клан не привязан. Сначала /setup #ТЕГ.",
        SetupFormat = "Формат: /setup #ТЕГ_КЛАНА",
        SetupOnlyAdmin = "Только админ группы может привязать клан.",
        SetupClanNotFound = "❌ Клан не найден. Проверь тег.",
        SetupOk = "✅ Клан «{0}» привязан к этой группе!\n\n"
                + "Участники: напишите боту /start в личку и отправьте свой тег CR — сразу увидите статистику.",
        SetupTopicNote = "\n\n📌 Напоминания и отчёты будут приходить в эту тему.",
        LinkFormat = "Формат: /link #ТВОЙ_ТЕГ",
        LinkNotFound = "❌ Игрок не найден. Проверь тег (профиль → значок тега).",
        LinkOkPrivate = "✅ Привязан игрок «{0}»! Открой Mini App через кнопку меню.",
        LinkOkGroup = "✅ Привязан игрок «{0}». Напишите боту /start в личку — буду присылать напоминания.",
        RemindOnlyAdmin = "Только админ группы может менять время напоминаний.",
        RemindFormat = "Формат: /remind N — за сколько часов до конца военного дня напоминать (от 1 до 12).\nНапример: /remind 3",
        RemindOk = "✅ Автонапоминания будут приходить за {0} ч до конца военного дня.\n"
                 + "Напомню только тем, кто к этому времени не отыграл все 4/4 колоды.",
        TopicOnlyAdmin = "Менять тему для уведомлений может только админ группы.",
        TopicSetToThread = "📌 Готово! Теперь напоминания, теги и отчёты бот будет слать в эту тему.",
        TopicSetToChat = "📌 Готово! Уведомления будут приходить в общий чат (не в тему). Запусти /settopic внутри нужной темы, чтобы привязать её.",
        NudgeOnlyAdmin = "Пинать игроков может только админ группы.",
        NudgeNoWarDay = "Сейчас не день войны — пинать некого.",
        NudgeAllPlayed = "Все уже отыграли 4/4 — пинать некого 🎉",
        NudgeNobodyTaggable = "{0} не доиграли, но никто из них не привязан — тегнуть некого.\n\n"
                            + "Привяжи их сам: ответь на сообщение игрока командой /bind #ТЕГ. Список: /unlinked",
        BindOnlyAdmin = "Привязывать игроков может только админ группы.",
        BindHelp = "Как привязать игрока:\n\n"
                 + "1) Ответь на любое сообщение человека командой:\n"
                 + "   /bind #ТЕГИГРОКА\n"
                 + "   Так подтянется и юзернейм, и аккаунт — это надёжнее.\n\n"
                 + "2) Или укажи юзернейм вручную:\n"
                 + "   /bind #ТЕГИГРОКА @username\n\n"
                 + "Посмотреть, кого ещё не привязали: /unlinked",
        BindBadUsername = "«{0}» не похоже на юзернейм Telegram.\n\n"
                        + "Юзернейм начинается с @, состоит из латиницы, цифр и подчёркиваний "
                        + "(например @qrt980). Посмотреть его можно в профиле человека.\n\n"
                        + "Надёжнее: ответь на любое сообщение игрока командой /bind #ТЕГ — "
                        + "тогда юзернейм подтянется сам.",
        BindWho = "Не понял, кого привязывать. Ответь этой командой на сообщение игрока "
                + "или укажи юзернейм: /bind #ТЕГ @username",
        BindTagNotFound = "Игрок с таким тегом не найден. Проверь тег.",
        BindNotInClan = "Этого тега нет в текущем составе клана.",
        BindOk = "✅ {0} привязан к {1}.\nТеперь бот будет тегать его в чате при /nudge и напоминаниях.",
        BindOkAccount = "аккаунту",
        BindMoved = "\n\n⚠️ Этот аккаунт был привязан к {0} — привязка перенесена. "
                  + "Если это разные люди, привяжи их по отдельности.",
        BindNoDm = "\n\n⚠️ В личные сообщения бот писать не сможет, пока игрок сам не нажмёт «Старт» у бота — "
                 + "Telegram запрещает писать первым. В чате тег работает.",
        ClaimBadLink = "Ссылка не подошла: она действует сутки и только один раз. "
                     + "Попроси у лидера новую.",
        ClaimTaken = "Этот тег уже привязан к другому аккаунту. Если это твой тег — "
                   + "напиши лидеру, он отвяжет старую привязку командой /unbind.",
        ClaimOk = "✅ Готово, ты привязан как {0}.\n\n"
                + "Теперь бот напомнит тебе про колоды КВ и тегнёт в чате, если забудешь. "
                + "Открой приложение кнопкой ниже — там твоя статистика и состав клана.",
        UnbindOnlyAdmin = "Отвязывать игроков может только админ группы.",
        UnbindNeedTag = "Укажи тег: /unbind #ТЕГИГРОКА",
        UnbindOk = "✅ Привязка {0} снята.",
        UnbindNothing = "Нечего снимать: либо тег не привязан, либо игрок привязался сам — такую привязку может убрать только он.",
        UnlinkedRosterFail = "Не удалось получить состав клана.",
        UnlinkedAllLinked = "Все привязаны 🎉 Бот сможет тегнуть каждого.",
        UnlinkedList = "👥 Ещё не привязаны ({0}):\n\n{1}\n\nПривяжи ответом на сообщение человека: /bind #ТЕГ",
        StatusNoWarData = "Не удалось получить данные войны.",
        StatusHeader = "⚔️ {0} — {1}",
        StatusPlayed = "Сыграли полностью: {0}/{1}",
        StatusHoursLeft = "До конца дня: ~{0} ч",
        StatusForecast = "🔮 Прогноз: {0} к концу дня, {1} за неделю",
        StatusMore = "\n… и ещё {0}. Полный список — в Mini App.",
        PeriodWarDay = "День войны",
        PeriodColosseum = "Колизей",
        PeriodTraining = "Тренировка",
        ErrCrApiToken = "⚠️ Clash Royale API отклонил запрос — ключ привязан к другому IP. Админ, проверь CLASH_ROYALE_API_TOKEN.",
        ErrCrApiDown = "⚠️ Clash Royale API недоступен. Попробуй через пару минут.",
        ErrDb = "⚠️ Ошибка базы данных: {0}",
        ErrGeneric = "⚠️ Ошибка: {0}",
        QuickNotFound = "❌ Игрок {0} не найден в Clash Royale.\n\n"
                      + "Проверь тег — он виден в профиле под именем (выглядит как #ABC123).\n"
                      + "Или отправь /start чтобы узнать подробнее.",
        QuickNoClan = "✅ Привязан: {0}\n\nТы сейчас не в клане — война недоступна.\n"
                    + "Открой Mini App через кнопку меню бота 🎮",
        QuickNoWarData = "✅ Привязан: {0}\nКлан: {1}\n\nДанные войны сейчас недоступны. Открой Mini App через кнопку меню 🎮",
        QuickHeader = "✅ {0}  •  {1}",
        QuickWarLine = "⚔️ {0} — до конца дня: ~{1} ч",
        QuickPlayed = "Отыграли сегодня: {0}/{1}",
        QuickMeAll = "Ты: ✅ все 4 колоды — молодец! Слава: {0} 🏆 (#{1})",
        QuickMeNone = "Ты: ❌ ещё не атаковал сегодня! Слава: {0} 🏆 (#{1})",
        QuickMeSome = "Ты: ⏳ {0}/4 колоды. Слава: {1} 🏆 (#{2})",
        QuickLaggardsTitle = "Не отыграли сегодня:",
        QuickLaggardRow = "  ❌ {0} ({1}/4)",
        QuickAndMore = "  … и ещё {0}",
        QuickNotInWar = "\nТебя нет в составе этой войны.",
        QuickTraining = "📋 Сейчас тренировочная неделя.",
        QuickMembers = "Участников в клане: {0}",
        QuickFooter = "Полная статистика — в Mini App: история, прогнозы, рейтинг 👇",
        QuickShareText = "⚔️ Слежу за Clan War через этот бот — отправь свой тег CR и сразу увидишь статистику войны своего клана",
        QuickShareButton = "📤 Поделиться с кланом",
        InlineWarTitle = "⚔️ Моя война",
        InlineWarDesc = "Медали, место в клане и колоды за сегодня",
        InlineWarText = "⚔️ {0} · {1}\n🏅 {2} медалей · {3} место в клане\n🃏 {4}/4 колод сегодня",
        InlineClanTitle = "🏰 Мой клан",
        InlineClanDesc = "Место в гонке недели и кто ещё не отыграл",
        InlineClanText = "🏰 {0}\n🏁 {1} место из {2} в гонке недели\n🏅 {3} медалей за неделю\n😴 не доиграли: {4}",
        InlineNoLinkTitle = "Аккаунт не привязан",
        InlineNoLinkDesc = "Открой бота и отправь свой тег — появится карточка",
        InlineNoLinkText = "⚔️ Слежу за Клановой войной через этого бота — кто не отыграл, сколько осталось времени и место клана в гонке.",
        InlineLinkButton = "Привязать аккаунт",
        InlineOpenBot = "⚔️ Открыть бота",
        InlineFooter = "\n\nСтатистика Клановой войны",
        InlineProfileTitle = "👤 Мой профиль",
        InlineProfileDesc = "Кубки, рекорд и победы в клановых войнах",
        InlineProfileText = "👤 {0} · {1} уровень\n🏆 {2} кубков (рекорд {3})\n⚔️ побед в КВ: {4} · 👑 три короны: {5}",
        InlineDeckTitle = "🃏 Моя колода",
        InlineDeckDesc = "Текущая колода — открывается в игре одним тапом",
        InlineDeckText = "🃏 Колода игрока {0}\n{1}\n\n📊 средний уровень: {2}",
        InlineDeckOpen = "🎮 Открыть колоду в игре",
        InlineTopTitle = "🔥 Топ клана за неделю",
        InlineTopDesc = "Кто больше всех набил медалей",
        InlineTopText = "🔥 Топ недели · {0}\n{1}",
        InlineLastWarTitle = "📜 Прошлая война",
        InlineLastWarDesc = "Чем закончилась предыдущая неделя",
        InlineLastWarText = "📜 {0} · прошлая война\n🏁 {1} место · 🏅 {2} медалей\n⚔️ КВ-трофеи: {3}",
        InlineFoundTitle = "🔍 Найденный игрок",
        InlineFoundDesc = "Профиль по введённому тегу",
        InlineNotFoundTitle = "Игрок не найден",
        InlineNotFoundDesc = "Проверь тег — он виден в профиле под именем",
        InlineNotFoundText = "❌ Игрок {0} не найден в Clash Royale.",
        InlineClanCardTitle = "🛡 Профиль клана",
        InlineClanCardDesc = "Очки, трофеи КВ, состав и порог входа",
        InlineClanCardText = "🛡 {0} · {1}\n👥 {2}/50 · 🏆 {3} очков клана\n⚔️ КВ-трофеи: {4} · вход от {5} кубков",
        InlineTopDecksTitle = "🌍 Колоды топ-игроков",
        InlineTopDecksDesc = "Чем играют лучшие в мире прямо сейчас",
        InlineTopDecksText = "🌍 Что играет мировой топ ({0} игроков)\n\n{1}",
        InlineTopDeckOne = "🎮 Открыть первую колоду",
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

        StartPrivate = "⚔️ Clanify — статистика війни Clash Royale\n\n"
                     + "Надішли свій тег акаунта CR прямо сюди — наприклад:\n"
                     + "#2VUPLPU0R\n\n"
                     + "Я одразу покажу:\n"
                     + "• хто не атакує у війні твого клану\n"
                     + "• твій особистий рахунок і місце в рейтингу\n"
                     + "• скільки годин лишилось до кінця дня\n\n"
                     + "Працює для всіх учасників — не лише для лідерів.\n\n"
                     + "Або відкрий Mini App кнопкою в меню нижче 👇",
        StartGroupNew = "⚔️ Clanify — статистика війни Clash Royale\n\n"
                      + "Щоб підключити клан до цієї групи, лідер або адміністратор виконує:\n"
                      + "/setup #ТЕГ_КЛАНУ\n\n"
                      + "Після цього кожен учасник може написати боту /start у ЛС і надіслати свій тег — і одразу побачить статистику.",
        StartGroupReady = "⚔️ Клан «{0}» підключено!\n"
                        + "/status — статус поточної війни\n"
                        + "/remind N — нагадування за N годин до кінця дня\n"
                        + "/nudge — розштовхати тих, хто не відіграв (тег за @username)\n"
                        + "/bind #ТЕГ — прив’язати гравця до Telegram (відповіддю на його повідомлення)\n"
                        + "/unlinked — кого ще не прив’язали\n"
                        + "/settopic — слати сповіщення в цю тему (запусти всередині теми)\n\n"
                        + "Учасники: напишіть боту /start у приват і надішліть свій тег CR.",
        OnlyInGroup = "⚠️ Команда працює лише в груповому чаті клану.",
        ClanNotLinked = "Клан не прив’язано. Спочатку /setup #ТЕГ.",
        SetupFormat = "Формат: /setup #ТЕГ_КЛАНУ",
        SetupOnlyAdmin = "Лише адмін групи може прив’язати клан.",
        SetupClanNotFound = "❌ Клан не знайдено. Перевір тег.",
        SetupOk = "✅ Клан «{0}» прив’язано до цієї групи!\n\n"
                + "Учасники: напишіть боту /start у приват і надішліть свій тег CR — одразу побачите статистику.",
        SetupTopicNote = "\n\n📌 Нагадування та звіти надходитимуть у цю тему.",
        LinkFormat = "Формат: /link #ТВІЙ_ТЕГ",
        LinkNotFound = "❌ Гравця не знайдено. Перевір тег (профіль → значок тега).",
        LinkOkPrivate = "✅ Прив’язано гравця «{0}»! Відкрий Mini App через кнопку меню.",
        LinkOkGroup = "✅ Прив’язано гравця «{0}». Напишіть боту /start у приват — надсилатиму нагадування.",
        RemindOnlyAdmin = "Лише адмін групи може змінювати час нагадувань.",
        RemindFormat = "Формат: /remind N — за скільки годин до кінця воєнного дня нагадувати (від 1 до 12).\nНаприклад: /remind 3",
        RemindOk = "✅ Автонагадування надходитимуть за {0} год до кінця воєнного дня.\n"
                 + "Нагадаю лише тим, хто до цього часу не відіграв усі 4/4 колоди.",
        TopicOnlyAdmin = "Змінювати тему для сповіщень може лише адмін групи.",
        TopicSetToThread = "📌 Готово! Тепер нагадування, теги та звіти бот надсилатиме в цю тему.",
        TopicSetToChat = "📌 Готово! Сповіщення надходитимуть у загальний чат (не в тему). Запусти /settopic усередині потрібної теми, щоб прив’язати її.",
        NudgeOnlyAdmin = "Розштовхувати гравців може лише адмін групи.",
        NudgeNoWarDay = "Зараз не день війни — розштовхувати нікого.",
        NudgeAllPlayed = "Усі вже відіграли 4/4 — розштовхувати нікого 🎉",
        NudgeNobodyTaggable = "{0} не дограли, але ніхто з них не прив’язаний — тегнути нікого.\n\n"
                            + "Прив’яжи їх сам: дай відповідь на повідомлення гравця командою /bind #ТЕГ. Список: /unlinked",
        BindOnlyAdmin = "Прив’язувати гравців може лише адмін групи.",
        BindHelp = "Як прив’язати гравця:\n\n"
                 + "1) Дай відповідь на будь-яке повідомлення людини командою:\n"
                 + "   /bind #ТЕГГРАВЦЯ\n"
                 + "   Так підтягнеться і юзернейм, і акаунт — це надійніше.\n\n"
                 + "2) Або вкажи юзернейм вручну:\n"
                 + "   /bind #ТЕГГРАВЦЯ @username\n\n"
                 + "Подивитися, кого ще не прив’язали: /unlinked",
        BindBadUsername = "«{0}» не схоже на юзернейм Telegram.\n\n"
                        + "Юзернейм починається з @, складається з латиниці, цифр і підкреслень "
                        + "(наприклад @qrt980). Подивитися його можна в профілі людини.\n\n"
                        + "Надійніше: дай відповідь на будь-яке повідомлення гравця командою /bind #ТЕГ — "
                        + "тоді юзернейм підтягнеться сам.",
        BindWho = "Не зрозумів, кого прив’язувати. Дай відповідь цією командою на повідомлення гравця "
                + "або вкажи юзернейм: /bind #ТЕГ @username",
        BindTagNotFound = "Гравця з таким тегом не знайдено. Перевір тег.",
        BindNotInClan = "Цього тега немає в поточному складі клану.",
        BindOk = "✅ {0} прив’язано до {1}.\nТепер бот тегатиме його в чаті при /nudge і нагадуваннях.",
        BindOkAccount = "акаунта",
        BindMoved = "\n\n⚠️ Цей акаунт був прив’язаний до {0} — прив’язку перенесено. "
                  + "Якщо це різні люди, прив’яжи їх окремо.",
        BindNoDm = "\n\n⚠️ В особисті повідомлення бот писати не зможе, поки гравець сам не натисне «Старт» у бота — "
                 + "Telegram забороняє писати першим. У чаті тег працює.",
        ClaimBadLink = "Посилання не підійшло: воно діє добу і лише один раз. "
                     + "Попроси в лідера нове.",
        ClaimTaken = "Цей тег уже прив’язаний до іншого акаунта. Якщо це твій тег — "
                   + "напиши лідеру, він відв’яже стару прив’язку командою /unbind.",
        ClaimOk = "✅ Готово, тебе прив’язано як {0}.\n\n"
                + "Тепер бот нагадає тобі про колоди КВ і тегне в чаті, якщо забудеш. "
                + "Відкрий застосунок кнопкою нижче — там твоя статистика і склад клану.",
        UnbindOnlyAdmin = "Відв’язувати гравців може лише адмін групи.",
        UnbindNeedTag = "Вкажи тег: /unbind #ТЕГГРАВЦЯ",
        UnbindOk = "✅ Прив’язку {0} знято.",
        UnbindNothing = "Нічого знімати: або тег не прив’язано, або гравець прив’язався сам — таку прив’язку може прибрати лише він.",
        UnlinkedRosterFail = "Не вдалося отримати склад клану.",
        UnlinkedAllLinked = "Усі прив’язані 🎉 Бот зможе тегнути кожного.",
        UnlinkedList = "👥 Ще не прив’язані ({0}):\n\n{1}\n\nПрив’яжи відповіддю на повідомлення людини: /bind #ТЕГ",
        StatusNoWarData = "Не вдалося отримати дані війни.",
        StatusHeader = "⚔️ {0} — {1}",
        StatusPlayed = "Зіграли повністю: {0}/{1}",
        StatusHoursLeft = "До кінця дня: ~{0} год",
        StatusForecast = "🔮 Прогноз: {0} до кінця дня, {1} за тиждень",
        StatusMore = "\n… і ще {0}. Повний список — у Mini App.",
        PeriodWarDay = "День війни",
        PeriodColosseum = "Колізей",
        PeriodTraining = "Тренування",
        ErrCrApiToken = "⚠️ Clash Royale API відхилив запит — ключ прив’язаний до іншого IP. Адміне, перевір CLASH_ROYALE_API_TOKEN.",
        ErrCrApiDown = "⚠️ Clash Royale API недоступний. Спробуй за кілька хвилин.",
        ErrDb = "⚠️ Помилка бази даних: {0}",
        ErrGeneric = "⚠️ Помилка: {0}",
        QuickNotFound = "❌ Гравця {0} не знайдено в Clash Royale.\n\n"
                      + "Перевір тег — він видно в профілі під іменем (виглядає як #ABC123).\n"
                      + "Або надішли /start, щоб дізнатися докладніше.",
        QuickNoClan = "✅ Прив’язано: {0}\n\nТи зараз не в клані — війна недоступна.\n"
                    + "Відкрий Mini App через кнопку меню бота 🎮",
        QuickNoWarData = "✅ Прив’язано: {0}\nКлан: {1}\n\nДані війни зараз недоступні. Відкрий Mini App через кнопку меню 🎮",
        QuickHeader = "✅ {0}  •  {1}",
        QuickWarLine = "⚔️ {0} — до кінця дня: ~{1} год",
        QuickPlayed = "Відіграли сьогодні: {0}/{1}",
        QuickMeAll = "Ти: ✅ усі 4 колоди — молодець! Слава: {0} 🏆 (#{1})",
        QuickMeNone = "Ти: ❌ ще не атакував сьогодні! Слава: {0} 🏆 (#{1})",
        QuickMeSome = "Ти: ⏳ {0}/4 колоди. Слава: {1} 🏆 (#{2})",
        QuickLaggardsTitle = "Не відіграли сьогодні:",
        QuickLaggardRow = "  ❌ {0} ({1}/4)",
        QuickAndMore = "  … і ще {0}",
        QuickNotInWar = "\nТебе немає у складі цієї війни.",
        QuickTraining = "📋 Зараз тренувальний тиждень.",
        QuickMembers = "Учасників у клані: {0}",
        QuickFooter = "Повна статистика — у Mini App: історія, прогнози, рейтинг 👇",
        QuickShareText = "⚔️ Стежу за Clan War через цього бота — надішли свій тег CR і одразу побачиш статистику війни свого клану",
        QuickShareButton = "📤 Поділитися з кланом",
        InlineWarTitle = "⚔️ Моя війна",
        InlineWarDesc = "Медалі, місце в клані та колоди за сьогодні",
        InlineWarText = "⚔️ {0} · {1}\n🏅 {2} медалей · {3} місце в клані\n🃏 {4}/4 колод сьогодні",
        InlineClanTitle = "🏰 Мій клан",
        InlineClanDesc = "Місце в гонці тижня і хто ще не відіграв",
        InlineClanText = "🏰 {0}\n🏁 {1} місце з {2} у гонці тижня\n🏅 {3} медалей за тиждень\n😴 не дограли: {4}",
        InlineNoLinkTitle = "Акаунт не прив’язано",
        InlineNoLinkDesc = "Відкрий бота та надішли свій тег — з’явиться картка",
        InlineNoLinkText = "⚔️ Стежу за Клановою війною через цього бота — хто не відіграв, скільки лишилось часу та місце клану в гонці.",
        InlineLinkButton = "Прив’язати акаунт",
        InlineOpenBot = "⚔️ Відкрити бота",
        InlineFooter = "\n\nСтатистика Кланової війни",
        InlineProfileTitle = "👤 Мій профіль",
        InlineProfileDesc = "Кубки, рекорд і перемоги в кланових війнах",
        InlineProfileText = "👤 {0} · {1} рівень\n🏆 {2} кубків (рекорд {3})\n⚔️ перемог у КВ: {4} · 👑 три корони: {5}",
        InlineDeckTitle = "🃏 Моя колода",
        InlineDeckDesc = "Поточна колода — відкривається в грі одним тапом",
        InlineDeckText = "🃏 Колода гравця {0}\n{1}\n\n📊 середній рівень: {2}",
        InlineDeckOpen = "🎮 Відкрити колоду в грі",
        InlineTopTitle = "🔥 Топ клану за тиждень",
        InlineTopDesc = "Хто найбільше набив медалей",
        InlineTopText = "🔥 Топ тижня · {0}\n{1}",
        InlineLastWarTitle = "📜 Минула війна",
        InlineLastWarDesc = "Чим завершився попередній тиждень",
        InlineLastWarText = "📜 {0} · минула війна\n🏁 {1} місце · 🏅 {2} медалей\n⚔️ КВ-трофеї: {3}",
        InlineFoundTitle = "🔍 Знайдений гравець",
        InlineFoundDesc = "Профіль за введеним тегом",
        InlineNotFoundTitle = "Гравця не знайдено",
        InlineNotFoundDesc = "Перевір тег — він видно в профілі під іменем",
        InlineNotFoundText = "❌ Гравця {0} не знайдено в Clash Royale.",
        InlineClanCardTitle = "🛡 Профіль клану",
        InlineClanCardDesc = "Очки, трофеї КВ, склад і поріг входу",
        InlineClanCardText = "🛡 {0} · {1}\n👥 {2}/50 · 🏆 {3} очок клану\n⚔️ КВ-трофеї: {4} · вхід від {5} кубків",
        InlineTopDecksTitle = "🌍 Колоди топ-гравців",
        InlineTopDecksDesc = "Чим грають найкращі у світі просто зараз",
        InlineTopDecksText = "🌍 Що грає світовий топ ({0} гравців)\n\n{1}",
        InlineTopDeckOne = "🎮 Відкрити першу колоду",
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

        StartPrivate = "⚔️ Clanify — Clash Royale war stats\n\n"
                     + "Send your CR account tag right here — for example:\n"
                     + "#2VUPLPU0R\n\n"
                     + "I'll show you straight away:\n"
                     + "• who isn't attacking in your clan's war\n"
                     + "• your own score and place in the rating\n"
                     + "• how many hours are left in the day\n\n"
                     + "Works for every member — not just leaders.\n\n"
                     + "Or open the Mini App with the menu button below 👇",
        StartGroupNew = "⚔️ Clanify — Clash Royale war stats\n\n"
                      + "To connect a clan to this group, the leader or an admin runs:\n"
                      + "/setup #CLANTAG\n\n"
                      + "After that every member can message the bot /start in DM and send their tag — and see the stats right away.",
        StartGroupReady = "⚔️ Clan \"{0}\" is connected!\n"
                        + "/status — current war status\n"
                        + "/remind N — reminders N hours before the day ends\n"
                        + "/nudge — nudge those who haven't played (tagged by @username)\n"
                        + "/bind #TAG — link a player to Telegram (reply to their message)\n"
                        + "/unlinked — who still isn't linked\n"
                        + "/settopic — send notifications to this topic (run it inside the topic)\n\n"
                        + "Members: message the bot /start in DM and send your CR tag.",
        OnlyInGroup = "⚠️ This command only works in the clan's group chat.",
        ClanNotLinked = "No clan linked. Run /setup #TAG first.",
        SetupFormat = "Format: /setup #CLANTAG",
        SetupOnlyAdmin = "Only a group admin can link the clan.",
        SetupClanNotFound = "❌ Clan not found. Check the tag.",
        SetupOk = "✅ Clan \"{0}\" is linked to this group!\n\n"
                + "Members: message the bot /start in DM and send your CR tag — you'll see the stats right away.",
        SetupTopicNote = "\n\n📌 Reminders and reports will arrive in this topic.",
        LinkFormat = "Format: /link #YOURTAG",
        LinkNotFound = "❌ Player not found. Check the tag (profile → the tag under your name).",
        LinkOkPrivate = "✅ Linked player \"{0}\"! Open the Mini App from the menu button.",
        LinkOkGroup = "✅ Linked player \"{0}\". Message the bot /start in DM — I'll send you reminders.",
        RemindOnlyAdmin = "Only a group admin can change the reminder time.",
        RemindFormat = "Format: /remind N — how many hours before the war day ends to remind (1 to 12).\nFor example: /remind 3",
        RemindOk = "✅ Auto-reminders will arrive {0}h before the war day ends.\n"
                 + "I'll only remind those who haven't played all 4/4 decks by then.",
        TopicOnlyAdmin = "Only a group admin can change the notification topic.",
        TopicSetToThread = "📌 Done! Reminders, tags and reports will now go to this topic.",
        TopicSetToChat = "📌 Done! Notifications will go to the main chat (not a topic). Run /settopic inside a topic to bind it.",
        NudgeOnlyAdmin = "Only a group admin can nudge players.",
        NudgeNoWarDay = "It's not a war day — nobody to nudge.",
        NudgeAllPlayed = "Everyone already played 4/4 — nobody to nudge 🎉",
        NudgeNobodyTaggable = "{0} haven't finished, but none of them are linked — nobody to tag.\n\n"
                            + "Link them yourself: reply to the player's message with /bind #TAG. List: /unlinked",
        BindOnlyAdmin = "Only a group admin can link players.",
        BindHelp = "How to link a player:\n\n"
                 + "1) Reply to any message from the person with:\n"
                 + "   /bind #PLAYERTAG\n"
                 + "   That picks up both the username and the account — more reliable.\n\n"
                 + "2) Or give the username manually:\n"
                 + "   /bind #PLAYERTAG @username\n\n"
                 + "To see who still isn't linked: /unlinked",
        BindBadUsername = "\"{0}\" doesn't look like a Telegram username.\n\n"
                        + "A username starts with @ and uses Latin letters, digits and underscores "
                        + "(for example @qrt980). You can find it in the person's profile.\n\n"
                        + "More reliable: reply to any message from the player with /bind #TAG — "
                        + "then the username is picked up automatically.",
        BindWho = "I couldn't tell who to link. Reply with this command to the player's message "
                + "or give the username: /bind #TAG @username",
        BindTagNotFound = "No player with that tag. Check the tag.",
        BindNotInClan = "That tag isn't in the clan's current roster.",
        BindOk = "✅ {0} is linked to {1}.\nThe bot will now tag them in chat for /nudge and reminders.",
        BindOkAccount = "the account",
        BindMoved = "\n\n⚠️ This account was linked to {0} — the link has been moved. "
                  + "If these are different people, link them separately.",
        BindNoDm = "\n\n⚠️ The bot can't send DMs until the player presses \"Start\" on the bot themselves — "
                 + "Telegram doesn't allow messaging first. Tagging in the chat works.",
        ClaimBadLink = "That link didn't work: it lasts 24 hours and only once. "
                     + "Ask your leader for a new one.",
        ClaimTaken = "This tag is already linked to another account. If it's your tag, "
                   + "ask your leader to unlink it with /unbind.",
        ClaimOk = "✅ Done, you're linked as {0}.\n\n"
                + "The bot will now remind you about your war decks and tag you in the chat if you forget. "
                + "Open the app with the button below — your stats and the clan roster are there.",
        UnbindOnlyAdmin = "Only a group admin can unlink players.",
        UnbindNeedTag = "Give a tag: /unbind #PLAYERTAG",
        UnbindOk = "✅ The link for {0} has been removed.",
        UnbindNothing = "Nothing to remove: either the tag isn't linked, or the player linked themselves — only they can undo that.",
        UnlinkedRosterFail = "Couldn't fetch the clan roster.",
        UnlinkedAllLinked = "Everyone is linked 🎉 The bot can tag them all.",
        UnlinkedList = "👥 Not linked yet ({0}):\n\n{1}\n\nLink them by replying to their message: /bind #TAG",
        StatusNoWarData = "Couldn't fetch the war data.",
        StatusHeader = "⚔️ {0} — {1}",
        StatusPlayed = "Played in full: {0}/{1}",
        StatusHoursLeft = "Day ends in: ~{0}h",
        StatusForecast = "🔮 Forecast: {0} by the end of the day, {1} for the week",
        StatusMore = "\n… and {0} more. Full list — in the Mini App.",
        PeriodWarDay = "War day",
        PeriodColosseum = "Colosseum",
        PeriodTraining = "Training",
        ErrCrApiToken = "⚠️ The Clash Royale API rejected the request — the key is bound to a different IP. Admin, check CLASH_ROYALE_API_TOKEN.",
        ErrCrApiDown = "⚠️ The Clash Royale API is unavailable. Try again in a couple of minutes.",
        ErrDb = "⚠️ Database error: {0}",
        ErrGeneric = "⚠️ Error: {0}",
        QuickNotFound = "❌ Player {0} not found in Clash Royale.\n\n"
                      + "Check the tag — it's shown in the profile under your name (looks like #ABC123).\n"
                      + "Or send /start to learn more.",
        QuickNoClan = "✅ Linked: {0}\n\nYou're not in a clan right now — war isn't available.\n"
                    + "Open the Mini App from the bot's menu button 🎮",
        QuickNoWarData = "✅ Linked: {0}\nClan: {1}\n\nWar data isn't available right now. Open the Mini App from the menu button 🎮",
        QuickHeader = "✅ {0}  •  {1}",
        QuickWarLine = "⚔️ {0} — day ends in: ~{1}h",
        QuickPlayed = "Played today: {0}/{1}",
        QuickMeAll = "You: ✅ all 4 decks — nice work! Fame: {0} 🏆 (#{1})",
        QuickMeNone = "You: ❌ haven't attacked today! Fame: {0} 🏆 (#{1})",
        QuickMeSome = "You: ⏳ {0}/4 decks. Fame: {1} 🏆 (#{2})",
        QuickLaggardsTitle = "Haven't played today:",
        QuickLaggardRow = "  ❌ {0} ({1}/4)",
        QuickAndMore = "  … and {0} more",
        QuickNotInWar = "\nYou're not in this war's roster.",
        QuickTraining = "📋 It's a training week right now.",
        QuickMembers = "Members in the clan: {0}",
        QuickFooter = "Full stats — in the Mini App: history, forecasts, rating 👇",
        QuickShareText = "⚔️ I track Clan War with this bot — send your CR tag and you'll see your clan's war stats right away",
        QuickShareButton = "📤 Share with the clan",
        InlineWarTitle = "⚔️ My war",
        InlineWarDesc = "Medals, place in the clan and decks today",
        InlineWarText = "⚔️ {0} · {1}\n🏅 {2} medals · #{3} in the clan\n🃏 {4}/4 decks today",
        InlineClanTitle = "🏰 My clan",
        InlineClanDesc = "Place in the week's race and who hasn't played",
        InlineClanText = "🏰 {0}\n🏁 place {1} of {2} in this week's race\n🏅 {3} medals this week\n😴 unfinished: {4}",
        InlineNoLinkTitle = "Account not linked",
        InlineNoLinkDesc = "Open the bot and send your tag — the card will appear",
        InlineNoLinkText = "⚔️ I track Clan War with this bot — who hasn't played, how much time is left and the clan's place in the race.",
        InlineLinkButton = "Link account",
        InlineOpenBot = "⚔️ Open the bot",
        InlineFooter = "\n\nClan War stats",
        InlineProfileTitle = "👤 My profile",
        InlineProfileDesc = "Trophies, personal best and Clan War wins",
        InlineProfileText = "👤 {0} · level {1}\n🏆 {2} trophies (best {3})\n⚔️ Clan War wins: {4} · 👑 three-crown: {5}",
        InlineDeckTitle = "🃏 My deck",
        InlineDeckDesc = "Current deck — opens in the game with one tap",
        InlineDeckText = "🃏 {0}'s deck\n{1}\n\n📊 average level: {2}",
        InlineDeckOpen = "🎮 Open deck in the game",
        InlineTopTitle = "🔥 Clan top of the week",
        InlineTopDesc = "Who scored the most medals",
        InlineTopText = "🔥 Top of the week · {0}\n{1}",
        InlineLastWarTitle = "📜 Last war",
        InlineLastWarDesc = "How the previous week ended",
        InlineLastWarText = "📜 {0} · last war\n🏁 place {1} · 🏅 {2} medals\n⚔️ war trophies: {3}",
        InlineFoundTitle = "🔍 Player found",
        InlineFoundDesc = "Profile for the tag you typed",
        InlineNotFoundTitle = "Player not found",
        InlineNotFoundDesc = "Check the tag — it's shown in the profile under the name",
        InlineNotFoundText = "❌ Player {0} not found in Clash Royale.",
        InlineClanCardTitle = "🛡 Clan profile",
        InlineClanCardDesc = "Score, war trophies, roster and entry requirement",
        InlineClanCardText = "🛡 {0} · {1}\n👥 {2}/50 · 🏆 {3} clan score\n⚔️ war trophies: {4} · entry from {5} trophies",
        InlineTopDecksTitle = "🌍 Top players' decks",
        InlineTopDecksDesc = "What the best in the world play right now",
        InlineTopDecksText = "🌍 What the world's top plays ({0} players)\n\n{1}",
        InlineTopDeckOne = "🎮 Open the first deck",
    };
}
