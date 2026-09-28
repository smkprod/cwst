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
    /// <summary>
    /// Первое, что бот говорит, когда его добавили в группу. Момент единственный,
    /// когда на него точно смотрят, поэтому текст отвечает ровно на два вопроса:
    /// что это и что делать дальше. Кто делает — разнесено по ролям: раньше команды
    /// админа и действия игрока шли одной стеной, и каждый думал, что это не к нему.
    /// </summary>
    public required string GroupJoined { get; init; }

    /// <summary>Бота вернули в группу, где клан уже был привязан.</summary>
    public required string GroupJoinedReady { get; init; }

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
    /// <summary>Сигнал: {0} - поражений подряд, {1} - « · −58🏆» или пусто.</summary>
    public required string TiltHead { get; init; }
    /// <summary>Дописка, когда после двух поражений меньше 45%.</summary>
    public required string TiltCoin { get; init; }
    /// <summary>{0} - карта, которая была у соперника в каждом бою серии.</summary>
    public required string TiltSameCard { get; init; }
    /// <summary>Советы через «|», идут по кругу.</summary>
    public required string TiltTips { get; init; }
    /// <summary>{0} - сколько бесплатных сигналов осталось.</summary>
    public required string TiltFreeLeft { get; init; }
    /// <summary>Последний бесплатный сигнал.</summary>
    public required string TiltFreeLast { get; init; }
    /// <summary>Кнопка паузы.</summary>
    public required string TiltBtnPause { get; init; }
    /// <summary>Кнопка «играю дальше».</summary>
    public required string TiltBtnGo { get; init; }
    /// <summary>Кнопка «сегодня не писать».</summary>
    public required string TiltBtnMute { get; init; }
    /// <summary>{0} - время конца паузы.</summary>
    public required string TiltPausedLine { get; init; }
    /// <summary>Ответ на «играю дальше».</summary>
    public required string TiltGoLine { get; init; }
    /// <summary>Ответ на «сегодня не писать».</summary>
    public required string TiltMutedLine { get; init; }
    /// <summary>{0} - главная карта колоды, {1} - % побед.</summary>
    public required string TiltResume { get; init; }
    /// <summary>Пауза закончилась, лучшей колоды не нашлось.</summary>
    public required string TiltResumeGeneric { get; init; }
    /// <summary>Заголовок итога захода.</summary>
    public required string TiltSummaryHead { get; init; }
    /// <summary>{0} побед, {1} поражений, {2} - « · +34🏆» или пусто.</summary>
    public required string TiltSummaryScore { get; init; }
    /// <summary>{0}–{1} после паузы.</summary>
    public required string TiltSummaryPause { get; init; }
    /// <summary>{0}–{1} после сигнала без паузы.</summary>
    public required string TiltSummaryAfter { get; init; }
    /// <summary>Остановился после сигнала.</summary>
    public required string TiltSummaryStopped { get; init; }
    /// <summary>{0}% после паузы, {1}% без паузы.</summary>
    public required string TiltSummaryWorks { get; init; }
    /// <summary>{0} - поражений сегодня.</summary>
    public required string TiltLimitHead { get; init; }
    /// <summary>Текст лимита на вечер.</summary>
    public required string TiltLimitBody { get; init; }
    /// <summary>{0} тип, {1}% после двух, {2}% обычно, {3} бесплатных сигналов.</summary>
    public required string TiltIntro { get; init; }
    /// <summary>Кнопка включить.</summary>
    public required string TiltIntroOn { get; init; }
    /// <summary>Кнопка не надо.</summary>
    public required string TiltIntroOff { get; init; }
    /// <summary>{0} - бесплатных сигналов.</summary>
    public required string TiltIntroEnabled { get; init; }
    /// <summary>Включено с Плюсом.</summary>
    public required string TiltIntroEnabledPlus { get; init; }
    /// <summary>Отказался.</summary>
    public required string TiltIntroDeclined { get; init; }
    /// <summary>Тип Лёд.</summary>
    public required string TiltTypeIce { get; init; }
    /// <summary>Тип Закипаешь.</summary>
    public required string TiltTypeBoiling { get; init; }
    /// <summary>Тип Вулкан.</summary>
    public required string TiltTypeVolcano { get; init; }
    /// <summary>{0} - кто подарил, {1} - до какой даты.</summary>
    public required string GiftReceived { get; init; }
    /// <summary>{0} - кому, {1} - до какой даты.</summary>
    public required string GiftSent { get; init; }
    /// <summary>{0} - дата конца.</summary>
    public required string PlusEnding { get; init; }
    /// <summary>Кнопка продлить.</summary>
    public required string PlusRenewButton { get; init; }
    /// <summary>«Стоп-тильт»: первая строка, {0} - поражений подряд.</summary>
    public required string TiltAlertHead { get; init; }
    /// <summary>Личная статистика: {0} - % побед после двух поражений, {1} - обычный %.</summary>
    public required string TiltAlertStats { get; init; }
    /// <summary>Когда личной статистики мало.</summary>
    public required string TiltAlertGeneric { get; init; }
    /// <summary>Совет и как выключить.</summary>
    public required string TiltAlertTail { get; init; }
    /// <summary>Мини-разбор после привязки: первая строка, {0} - имя.</summary>
    public required string LinkedHead { get; init; }
    /// <summary>{0} боёв, {1}% побед, {2} побед, {3} поражений.</summary>
    public required string LinkedStats { get; init; }
    /// <summary>{0} - карта, {1}% побед, {2} боёв.</summary>
    public required string LinkedTough { get; init; }
    /// <summary>{0}% побед после двух поражений подряд.</summary>
    public required string LinkedTilt { get; init; }
    /// <summary>Триал выдан: {0} - дней.</summary>
    public required string LinkedTrial { get; init; }
    /// <summary>Боёв для разбора пока нет.</summary>
    public required string LinkedNoBattles { get; init; }
    /// <summary>Призыв открыть приложение.</summary>
    public required string LinkedTail { get; init; }
    /// <summary>Строка для игрока без клана.</summary>
    public required string LinkedNoClanLine { get; init; }
    /// <summary>Кнопка открыть приложение.</summary>
    public required string OpenAppButton { get; init; }
    /// <summary>Кнопка открыть разбор.</summary>
    public required string OpenReviewButton { get; init; }
    /// <summary>Чужой тег: {0} - имя, {1} - тег.</summary>
    public required string ForeignTagHead { get; init; }
    /// <summary>{0} - текущий тег.</summary>
    public required string ForeignTagLinkedAs { get; init; }
    /// <summary>Кнопка «это мой аккаунт».</summary>
    public required string ForeignTagRelinkButton { get; init; }
    /// <summary>{0} - имя после перепривязки.</summary>
    public required string RelinkDone { get; init; }
    /// <summary>Не вышло перепривязать.</summary>
    public required string RelinkFailed { get; init; }
    /// <summary>Справка /help.</summary>
    public required string HelpText { get; init; }
    /// <summary>Команде нужен привязанный тег.</summary>
    public required string NotLinkedYet { get; init; }
    /// <summary>{0} - средний эликсир, {1} - карты.</summary>
    public required string DeckHead { get; init; }
    /// <summary>Колоды нет.</summary>
    public required string DeckNone { get; init; }
    /// <summary>Кнопка открыть колоду в игре.</summary>
    public required string DeckOpenButton { get; init; }
    /// <summary>{0} - боёв в окне.</summary>
    public required string MetaHead { get; init; }
    /// <summary>{0} - место, {1}% побед, {2} игр, {3} - карты.</summary>
    public required string MetaRow { get; init; }
    /// <summary>Меты ещё нет.</summary>
    public required string MetaEmpty { get; init; }
    /// <summary>Что даёт Плюс; {0} - строка статуса.</summary>
    public required string PlusInfo { get; init; }
    /// <summary>{0} - до какой даты.</summary>
    public required string PlusActiveLine { get; init; }
    /// <summary>{0} - цена 7 дней, {1} - цена 30 дней.</summary>
    public required string PlusOfferLine { get; init; }
    /// <summary>Платное выключено.</summary>
    public required string PlusFreeLine { get; init; }
    /// <summary>Кнопка открыть Плюс.</summary>
    public required string PlusButton { get; init; }
    /// <summary>Помощь с оплатой; {0} - кому писать.</summary>
    public required string PaySupport { get; init; }
    /// <summary>Если контакт владельца не задан.</summary>
    public required string PaySupportOwnerFallback { get; init; }
    /// <summary>Условия.</summary>
    public required string Terms { get; init; }

    /* --- Трекер боёв --- */
    /// <summary>Карточка захода: {0} - начало, {1}–{2} - счёт, {3} - кубки со знаком.</summary>
    public required string TrkCardHead { get; init; }
    /// <summary>Последний бой: {0} - ✅/❌/➖, {1}–{2} короны, {3} кубки, {4} соперник, {5} архетип.</summary>
    public required string TrkLast { get; init; }
    /// <summary>{0} - утекло эликсира.</summary>
    public required string TrkVAfk { get; init; }
    /// <summary>{0} - на сколько уровней, {1} - карта, {2} - её уровень.</summary>
    public required string TrkVLevels { get; init; }
    /// <summary>{0} - HP его башни.</summary>
    public required string TrkVClose { get; init; }
    /// <summary>{0} - утекло, {1} - обычно.</summary>
    public required string TrkVLeak { get; init; }
    /// <summary>Причины не видно.</summary>
    public required string TrkVEven { get; init; }
    /// <summary>{0} - разница уровней со знаком.</summary>
    public required string TrkVWinLevels { get; init; }
    /// <summary>{0} - на сколько кубков соперник выше.</summary>
    public required string TrkVWinUpset { get; init; }
    /// <summary>{0} - место соперника в мире.</summary>
    public required string TrkVWinRank { get; init; }
    /// <summary>{0} - HP своей башни.</summary>
    public required string TrkVWinClose { get; init; }
    /// <summary>Плюс: {0} - архетип, {1}–{2} - счёт за месяц, {3} - обычный %.</summary>
    public required string TrkPVsArch { get; init; }
    /// <summary>Плюс: {0} - архетип, {1} - главная карта другой колоды, {2}–{3} - её счёт.</summary>
    public required string TrkPBetterDeck { get; init; }
    /// <summary>{0} - до скольки пауза.</summary>
    public required string TrkTiltPause { get; init; }
    /// <summary>Строка боя над тильт-сигналом: {0} - номер боя, {1}–{2} короны, {3} архетип, {4} кубки.</summary>
    public required string TrkAlertPrefix { get; init; }
    /// <summary>Итог: {0}–{1} - время, {2}–{3} - счёт, {4} - кубки.</summary>
    public required string TrkSumHead { get; init; }
    /// <summary>{0}–{1} короны, {2} - соперник, {3} - кубки.</summary>
    public required string TrkSumBest { get; init; }
    /// <summary>{0} - архетип, {1}–{2} - счёт за заход.</summary>
    public required string TrkSumWorst { get; init; }
    /// <summary>{0} - средняя разница в поражениях, {1} - в победах.</summary>
    public required string TrkSumLevels { get; init; }
    /// <summary>{0} - после какого боя, {1}–{2} - что было дальше, {3} - кубки.</summary>
    public required string TrkSumMoment { get; init; }
    /// <summary>{0} - сколько подсказок Плюса было за заход.</summary>
    public required string TrkSumLocked { get; init; }
    /// <summary>Карточки выключены до конца дня.</summary>
    public required string TrkMuted { get; init; }
    /// <summary>Старая карточка, когда новая ниже.</summary>
    public required string TrkBelow { get; init; }
    /// <summary>Трекер включён.</summary>
    public required string TrkOn { get; init; }
    /// <summary>Трекер выключен.</summary>
    public required string TrkOff { get; init; }
    /// <summary>{0} - включён/выключен.</summary>
    public required string TrkStatus { get; init; }
    /// <summary>Слово «включён».</summary>
    public required string TrkStatusOn { get; init; }
    /// <summary>Слово «выключен».</summary>
    public required string TrkStatusOff { get; init; }
    /// <summary>Трекер пока в закрытом тесте.</summary>
    public required string TrkUnavailable { get; init; }
    /// <summary>Колода вне архетипов: {0} - её самая дорогая карта.</summary>
    public required string TrkOther { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnReport { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnAll { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnMute { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnSession { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnPlus { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnOn { get; init; }
    /// <summary>Кнопка.</summary>
    public required string TrkBtnOff { get; init; }
    /// <summary>Ответ на /tracker в группе: трекер живёт в личке.</summary>
    public required string TrkInGroup { get; init; }
    /// <summary>Кнопка: открыть трекер в личке.</summary>
    public required string TrkBtnDm { get; init; }

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
            "🏆 {0} — 900 за день. Четыре боя, четыре трупа, ноль свидетелей",
            "👑 900/900 у {0}. Соперники сменили ник и ушли в другой клан 📝",
            "🚀 {0} закрыл день на 900. Где-то в Supercell нервно пересчитывают баланс карт",
            "💪 {0}: 900 из 900. Даже башня не поняла, за что ей прилетело",
            "⚡ 900 за день от {0}. Противники до сих пор ищут кнопку «сдаться»",
            "🔥 {0} сделал идеальный день. Сегодня он играл, остальные — присутствовали",
            "🎯 900 у {0}. Три короны стали его личным почерком ✍️",
            "🧊 {0} — 900 за день, не моргнув. Ледяное спокойствие и чужие слёзы",
            "📈 {0} набил 900. Клан растёт, соперники — в терапии",
            "🛡️ 900/900 от {0}. Защита была, просто она не пригодилась",
            "🤖 {0} закрыл 4/4 на максимум. Подозрительно ровно. Проверьте, человек ли он",
            "🍿 {0} — 900 за день. Остальные могли не играть, а просто посмотреть",
        ],

        RespectTitle = "👏 <b>Респекты дня</b>",
        RespectFooter = "<i>Всего за сегодня: {0}. Респект можно дать раз в день — загляни в приложение.</i>",

        SmartAlert = "📉 Без твоих атак шанс клана на победу упадёт с {0}% до {1}%!\n"
                   + "Осталось колод: {2}/4 — успей сыграть.",


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

        StartPrivate = "⚔️ Clanify — твой помощник в Clash Royale\n\nПришли свой тег — например #2VUPLPU0R. Клан не нужен.\n\nЧто я сделаю:\n• запомню твои бои и покажу, против каких карт ты проседаешь\n• посчитаю тильт, утечку эликсира и лучшее время для игры\n• покажу, чем сейчас выигрывает топ-500 мира\n\nА если ты в клане — напомню про колоды КВ и посчитаю вклад каждого 🏰\n\nВсе команды: /help",
        GroupJoined = "👋 Привет! Я Clanify, бот для клановых войн Clash Royale.\n\n"
                    + "Я открываюсь как приложение прямо в Telegram, устанавливать ничего не нужно.\n\n"
                    + "Осталось два шага:\n\n"
                    + "1️⃣ АДМИН ГРУППЫ пишет сюда:\n"
                    + "/setup #ТЕГ_КЛАНА\n"
                    + "Тег есть в игре, в профиле клана.\n\n"
                    + "2️⃣ КАЖДЫЙ ИГРОК жмёт кнопку под этим сообщением и привязывает себя.\n\n"
                    + "После этого я показываю кто отыграл войну, а кто нет, и напоминаю забывшим.",
        GroupJoinedReady = "👋 Снова здесь! Клан «{0}» уже подключён к этой группе.\n\n"
                         + "Кто ещё не привязал себя — жмите кнопку под сообщением.",
        StartGroupNew = "⚔️ Clanify — статистика войны Clash Royale\n\n"
                      + "1️⃣ Админ группы: /setup #ТЕГ_КЛАНА\n"
                      + "2️⃣ Игроки: кнопка под сообщением\n\n"
                      + "Тег клана есть в игре, в профиле клана.",
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
        SetupOk = "✅ Клан «{0}» подключён!\n\n"
                + "Теперь каждый игрок жмёт кнопку под этим сообщением и привязывает себя.\n"
                + "Это займёт полминуты и нужно сделать один раз.\n\n"
                + "Кто не привязался, того я не вижу в статистике и не могу напомнить о войне.",
        SetupTopicNote = "\n\n📌 Напоминания и отчёты будут приходить в эту тему.",
        LinkFormat = "Формат: /link #ТВОЙ_ТЕГ",
        LinkNotFound = "❌ Игрок не найден. Проверь тег (профиль → значок тега).",
        LinkOkPrivate = "✅ Привязан игрок «{0}»! Открой Mini App через кнопку меню.",
        LinkOkGroup = "✅ Привязан игрок «{0}».\n\n"
                    + "Чтобы я мог писать напоминания лично, открой приложение кнопкой ниже.",
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
        ErrDb = "⚠️ Что-то пошло не так на нашей стороне. Я уже записал ошибку, попробуй ещё раз через минуту.",
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
        TiltHead = "🧊 Стоп-тильт · поражений подряд: {0}{1}",
        TiltCoin = " Это хуже монетки 🪙",
        TiltSameCard = "Каждый раз у соперника был «{0}».",
        TiltTips = "Пауза 10–15 минут — и шансы вернутся.|Выдохни, выпей воды и вернись с холодной головой.|Даже топ-1000 делает паузы после двух сливов.|Серия — не приговор. Пауза — тоже часть игры.|Кубки никуда не денутся, если отойти на 15 минут.",
        TiltFreeLeft = "Бесплатных сигналов осталось: {0}.",
        TiltFreeLast = "Это последний бесплатный сигнал — дальше с Плюсом.",
        TiltBtnPause = "⏸ Пауза 15 мин",
        TiltBtnGo = "▶️ Играю дальше",
        TiltBtnMute = "🔕 Сегодня не писать",
        TiltPausedLine = "⏸ Пауза до {0}. Напишу, когда можно.",
        TiltGoLine = "▶️ Понял, молчу до конца захода.",
        TiltMutedLine = "🔕 Сегодня больше не пишу.",
        TiltResume = "🟢 Можно. Начни с колоды с «{0}» — {1}% побед за месяц.",
        TiltResumeGeneric = "🟢 Пауза закончилась — можно играть. Удачи!",
        TiltSummaryHead = "🧊 Стоп-тильт · итог захода",
        TiltSummaryScore = "{0}–{1}{2}",
        TiltSummaryPause = "⏸ Пауза → после неё {0}–{1}",
        TiltSummaryAfter = "После сигнала: {0}–{1}",
        TiltSummaryStopped = "✅ Ты остановился после сигнала — кубки целы.",
        TiltSummaryWorks = "После паузы ты выигрываешь {0}%, без паузы — {1}%.",
        TiltLimitHead = "🛑 Лимит на вечер: поражений сегодня — {0}.",
        TiltLimitBody = "Ты сам поставил этот лимит. Может, на сегодня хватит?",
        TiltIntro = "🧊 Готово: твой тильт-тип — {0}.\nПосле двух поражений подряд ты выигрываешь {1}%, обычно — {2}%.\n\nНаписать тебе прямо во время игры, когда начнётся серия? Первые {3} раза — бесплатно.",
        TiltIntroOn = "🧊 Включить",
        TiltIntroOff = "Не надо",
        TiltIntroEnabled = "✅ Включено. Напишу, когда начнётся серия. Бесплатных сигналов: {0}.",
        TiltIntroEnabledPlus = "✅ Включено. Напишу, когда начнётся серия.",
        TiltIntroDeclined = "Ок, не буду. Включить можно в приложении: «Я» → «🧊 Стоп-тильт».",
        TiltTypeIce = "🧊 Лёд",
        TiltTypeBoiling = "🌡 Закипаешь",
        TiltTypeVolcano = "🌋 Вулкан",
        GiftReceived = "🎁 {0} подарил тебе Clanify Плюс до {1}!\n\n🧊 Стоп-тильт уже включён: напишу, когда начнёшь сливать серию.",
        GiftSent = "🎁 Подарок отправлен: {0} получил Плюс до {1}. Спасибо!",
        PlusEnding = "⏳ Твой Clanify Плюс закончится {0}. Продлить — одним нажатием 👇",
        PlusRenewButton = "💎 Продлить",
        TiltAlertHead = "🧊 Стоп-тильт: поражений подряд — {0}.",
        TiltAlertStats = "По твоим же боям после двух поражений подряд ты выигрываешь {0}%, а обычно — {1}%.",
        TiltAlertGeneric = "После серии поражений легко заиграться и слить ещё — дай голове остыть.",
        TiltAlertTail = "Пауза 10–15 минут: вода, пара минут без телефона — и назад с холодной головой.\n\nВыключить: «Я» → «⚔️ Разбор боёв».",
        LinkedHead = "✅ Привязал: {0}",
        LinkedStats = "⚔️ Последние {0} боёв: {1}% побед ({2}–{3})",
        LinkedTough = "🎯 Сложнее всего против «{0}»: {1}% побед в {2} боях",
        LinkedTilt = "🧊 После двух поражений подряд: {0}% побед",
        LinkedTrial = "🎁 Включил тебе Плюс на {0} дн. бесплатно: полный разбор и «Стоп-тильт» — напишу, когда пора сделать паузу.",
        LinkedNoBattles = "Боёв 1 на 1 в журнале пока нет — сыграй пару боёв, и я начну разбор.",
        LinkedTail = "Полный разбор — в приложении: против чего проигрываешь, когда играешь лучше, мета топа 👇",
        LinkedNoClanLine = "Ты не в клане — война недоступна, но разбор боёв работает и без клана.",
        OpenAppButton = "🎮 Открыть приложение",
        OpenReviewButton = "📊 Открыть разбор",
        ForeignTagHead = "👤 {0} · {1}",
        ForeignTagLinkedAs = "Ты привязан как {0}. Если это твой второй аккаунт — нажми кнопку, и привязка переедет на него. Посмотреть игрока подробнее можно в приложении → «Поиск».",
        ForeignTagRelinkButton = "🔗 Это мой аккаунт",
        RelinkDone = "✅ Теперь ты привязан как {0}.",
        RelinkFailed = "Не получилось перепривязать — пришли тег ещё раз.",
        HelpText = "📖 Что я умею\n\nПришли свой тег (например #2VUPLPU0R) — привяжу и разберу твои бои. Клан не нужен.\n\n/me — короткий разбор твоих боёв\n/deck — твоя колода из последнего боя\n/meta — лучшие колоды топа за неделю\n/tracker — трекер боёв: разбор после каждого боя\n/plus — Clanify Плюс\n/paysupport — помощь с оплатой\n/terms — условия\n\nВ группе клана: /setup #ТЕГ_КЛАНА — подключить войну, /status — кто не доиграл.",
        NotLinkedYet = "Сначала пришли свой тег — например #2VUPLPU0R.",
        DeckHead = "🃏 Твоя колода из последнего боя (💧{0}):\n{1}",
        DeckNone = "Не нашёл боёв 1 на 1 в журнале — сыграй бой, и я покажу колоду.",
        DeckOpenButton = "🃏 Открыть в игре",
        MetaHead = "🔥 Лучшие колоды топа за неделю (боёв: {0})",
        MetaRow = "{0}. {1}% побед · {2} игр\n{3}",
        MetaEmpty = "Мета топа ещё собирается — загляни завтра.",
        PlusInfo = "💎 Clanify Плюс\n\n🧊 Стоп-тильт: напишу «стоп» прямо во время игры, когда начнёшь сливать серию, — с твоими же цифрами, кнопкой паузы и итогом захода.\n🛑 Свои правила: после 2 или 3 поражений, лимит на вечер, тихие часы.\n🔬 И ещё: полный разбор боёв и контры к твоим колодам по боям топа.\n\n{0}",
        PlusActiveLine = "✅ Плюс активен до {0}.",
        PlusOfferLine = "7 дней — {0}⭐ · 30 дней — {1}⭐. Разовый пропуск, без автопродления. Купить — в приложении 👇",
        PlusFreeLine = "Сейчас всё открыто бесплатно — пользуйся 🙂",
        PlusButton = "💎 Открыть Плюс",
        PaySupport = "💬 Помощь с оплатой\n\nЗвёзды списались, а Плюс или спонсорство не включились? Хочешь вернуть звёзды? Напиши {0} и перешли сообщение об оплате — в нём номер платежа. По нему всё включат вручную или вернут звёзды.",
        PaySupportOwnerFallback = "владельцу бота",
        Terms = "📄 Условия\n\nClanify — неофициальный фан-проект, не связан с Supercell и не одобрен ею. Данные — из официального Clash Royale API.\n\nПлюс и спонсорство — разовые цифровые пропуска на указанный срок, без автопродления. Оплата — звёздами Telegram. Передумал — вернём звёзды без вопросов в течение 48 часов после оплаты: /paysupport. Если что-то не работает — тоже туда, включим вручную.\n\nМы храним твой тег, бои за 30 дней и настройки уведомлений — только для работы бота.",
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
        TrkCardHead = "🎯 Заход с {0} · {1}–{2} · {3}🏆",
        TrkLast = "{0} {1}–{2} · {3}🏆 · {4} · {5}",
        TrkVAfk = "💡 Похоже на вылет или АФК: утекло {0} эликсира.",
        TrkVLevels = "💡 Соперник прокачан сильнее: +{0} уровня в среднем. Ниже всего у тебя «{1}» ({2}).",
        TrkVClose = "💡 Близко: у его башни оставалось {0} HP.",
        TrkVLeak = "💡 Утекло {0} эликсира — обычно у тебя {1}.",
        TrkVEven = "💡 Уровни и колоды на равных — решилось в самом бою. Повтор есть в журнале боёв в игре.",
        TrkVWinLevels = "💡 Победа с уровнями {0} — сильно.",
        TrkVWinUpset = "💡 Обыграл соперника на {0}🏆 выше.",
        TrkVWinRank = "💡 Обыграл №{0} в мире.",
        TrkVWinClose = "💡 Вытащил: у твоей башни оставалось {0} HP.",
        TrkPVsArch = "Против {0} за месяц {1}–{2} (обычно ты {3}%)",
        TrkPBetterDeck = "Против {0} лучше идёт твоя колода с «{1}»: {2}–{3}",
        TrkTiltPause = "🧊 Пауза до {0}",
        TrkAlertPrefix = "Бой {0}: {1}–{2} против {3} · {4}🏆",
        TrkSumHead = "🏁 Заход {0}–{1} · {2}–{3} · {4}🏆",
        TrkSumBest = "⭐ Лучший: {0}–{1} против {2} ({3}🏆)",
        TrkSumWorst = "😖 Тяжелее всего: {0} — {1}–{2} за вечер",
        TrkSumLevels = "Уровни: в поражениях {0}, в победах {1}",
        TrkSumMoment = "🧊 Стоп-тильт остановил бы тебя после {0}-го боя — дальше было {1}–{2}, {3}🏆",
        TrkSumLocked = "🔒 За заход {0} подсказки «против кого и чем играть» — в Плюсе",
        TrkMuted = "🔕 Сегодня без карточек. Бои всё равно сохраняются в историю.",
        TrkBelow = "↓ Карточка захода ниже",
        TrkOn = "🎯 Трекер боёв включён. После каждого боя я тихо обновляю одну карточку захода — без звука. Разбор каждого боя и история — в приложении. Выключить — /tracker.",
        TrkOff = "Трекер выключен. История боёв в приложении остаётся.",
        TrkStatus = "🎯 Трекер боёв: {0}\n\nПосле каждого боя — тихая карточка захода: счёт, соперник, что решило бой. Все бои с разбором — в приложении.",
        TrkStatusOn = "включён ✅",
        TrkStatusOff = "выключен",
        TrkUnavailable = "🎯 Трекер боёв пока в закрытом тесте — скоро откроем всем.",
        TrkOther = "колода с «{0}»",
        TrkBtnReport = "📖 Разбор",
        TrkBtnAll = "📜 Все бои",
        TrkBtnMute = "🔕 Сегодня без карточек",
        TrkBtnSession = "📜 Разбор захода",
        TrkBtnPlus = "⭐ Плюс",
        TrkBtnOn = "Включить",
        TrkBtnOff = "Выключить",
        TrkInGroup = "🎯 Трекер боёв работает в личке: там я после каждого боя тихо присылаю разбор. Нажми кнопку ниже — и включи в один тап.",
        TrkBtnDm = "🎯 Включить в личке",
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
            "🏆 {0} — 900 за день. Чотири бої, чотири трупи, жодного свідка",
            "👑 900/900 у {0}. Суперники змінили нік і пішли в інший клан 📝",
            "🚀 {0} закрив день на 900. Десь у Supercell нервово перераховують баланс карт",
            "💪 {0}: 900 із 900. Навіть вежа не зрозуміла, за що їй прилетіло",
            "⚡ 900 за день від {0}. Суперники досі шукають кнопку «здатися»",
            "🔥 {0} зробив ідеальний день. Сьогодні він грав, решта — була присутня",
            "🎯 900 у {0}. Три корони стали його особистим почерком ✍️",
            "🧊 {0} — 900 за день, і оком не змигнув. Крижаний спокій і чужі сльози",
            "📈 {0} набив 900. Клан росте, суперники — на терапії",
            "🛡️ 900/900 від {0}. Захист був, просто не знадобився",
            "🤖 {0} закрив 4/4 на максимум. Підозріло рівно. Перевірте, чи він людина",
            "🍿 {0} — 900 за день. Решта могла не грати, а просто подивитися",
        ],

        RespectTitle = "👏 <b>Респекти дня</b>",
        RespectFooter = "<i>Усього за сьогодні: {0}. Респект можна дати раз на день — зазирни в застосунок.</i>",

        SmartAlert = "📉 Без твоїх атак шанс клану на перемогу впаде з {0}% до {1}%!\n"
                   + "Залишилось колод: {2}/4 — устигни зіграти.",


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

        StartPrivate = "⚔️ Clanify — твій помічник у Clash Royale\n\nНадішли свій тег — наприклад #2VUPLPU0R. Клан не потрібен.\n\nЩо я зроблю:\n• запам’ятаю твої бої й покажу, проти яких карт ти просідаєш\n• порахую тільт, витік еліксиру й найкращий час для гри\n• покажу, чим зараз виграє топ-500 світу\n\nА якщо ти в клані — нагадаю про колоди КВ і порахую внесок кожного 🏰\n\nУсі команди: /help",
        GroupJoined = "👋 Привіт! Я Clanify, бот для кланових воєн Clash Royale.\n\n"
                    + "Я відкриваюся як застосунок прямо в Telegram, встановлювати нічого не треба.\n\n"
                    + "Лишилося два кроки:\n\n"
                    + "1️⃣ АДМІН ГРУПИ пише сюди:\n"
                    + "/setup #ТЕГ_КЛАНУ\n"
                    + "Тег є у грі, у профілі клану.\n\n"
                    + "2️⃣ КОЖЕН ГРАВЕЦЬ тисне кнопку під цим повідомленням і прив’язує себе.\n\n"
                    + "Після цього я показую хто відіграв війну, а хто ні, і нагадую тим, хто забув.",
        GroupJoinedReady = "👋 Знову тут! Клан «{0}» уже підключено до цієї групи.\n\n"
                         + "Хто ще не прив’язав себе — тисніть кнопку під повідомленням.",
        StartGroupNew = "⚔️ Clanify — статистика війни Clash Royale\n\n"
                      + "1️⃣ Адмін групи: /setup #ТЕГ_КЛАНУ\n"
                      + "2️⃣ Гравці: кнопка під повідомленням\n\n"
                      + "Тег клану є у грі, у профілі клану.",
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
        SetupOk = "✅ Клан «{0}» підключено!\n\n"
                + "Тепер кожен гравець тисне кнопку під цим повідомленням і прив’язує себе.\n"
                + "Це займе пів хвилини і потрібно зробити один раз.\n\n"
                + "Хто не прив’язався, того я не бачу в статистиці й не можу нагадати про війну.",
        SetupTopicNote = "\n\n📌 Нагадування та звіти надходитимуть у цю тему.",
        LinkFormat = "Формат: /link #ТВІЙ_ТЕГ",
        LinkNotFound = "❌ Гравця не знайдено. Перевір тег (профіль → значок тега).",
        LinkOkPrivate = "✅ Прив’язано гравця «{0}»! Відкрий Mini App через кнопку меню.",
        LinkOkGroup = "✅ Прив’язано гравця «{0}».\n\n"
                    + "Щоб я міг писати нагадування особисто, відкрий застосунок кнопкою нижче.",
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
        ErrDb = "⚠️ Щось пішло не так на нашому боці. Я вже записав помилку, спробуй ще раз за хвилину.",
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
        TiltHead = "🧊 Стоп-тільт · поразок поспіль: {0}{1}",
        TiltCoin = " Це гірше за монетку 🪙",
        TiltSameCard = "Щоразу в суперника був «{0}».",
        TiltTips = "Пауза 10–15 хвилин — і шанси повернуться.|Видихни, випий води й повернися з холодною головою.|Навіть топ-1000 робить паузи після двох поразок.|Серія — не вирок. Пауза — теж частина гри.|Кубки нікуди не дінуться, якщо відійти на 15 хвилин.",
        TiltFreeLeft = "Безкоштовних сигналів лишилося: {0}.",
        TiltFreeLast = "Це останній безкоштовний сигнал — далі з Плюсом.",
        TiltBtnPause = "⏸ Пауза 15 хв",
        TiltBtnGo = "▶️ Граю далі",
        TiltBtnMute = "🔕 Сьогодні не писати",
        TiltPausedLine = "⏸ Пауза до {0}. Напишу, коли можна.",
        TiltGoLine = "▶️ Зрозумів, мовчу до кінця заходу.",
        TiltMutedLine = "🔕 Сьогодні більше не пишу.",
        TiltResume = "🟢 Можна. Почни з колоди з «{0}» — {1}% перемог за місяць.",
        TiltResumeGeneric = "🟢 Пауза скінчилася — можна грати. Успіху!",
        TiltSummaryHead = "🧊 Стоп-тільт · підсумок заходу",
        TiltSummaryScore = "{0}–{1}{2}",
        TiltSummaryPause = "⏸ Пауза → після неї {0}–{1}",
        TiltSummaryAfter = "Після сигналу: {0}–{1}",
        TiltSummaryStopped = "✅ Ти зупинився після сигналу — кубки цілі.",
        TiltSummaryWorks = "Після паузи ти виграєш {0}%, без паузи — {1}%.",
        TiltLimitHead = "🛑 Ліміт на вечір: поразок сьогодні — {0}.",
        TiltLimitBody = "Ти сам поставив цей ліміт. Може, на сьогодні досить?",
        TiltIntro = "🧊 Готово: твій тільт-тип — {0}.\nПісля двох поразок поспіль ти виграєш {1}%, зазвичай — {2}%.\n\nНаписати тобі просто під час гри, коли почнеться серія? Перші {3} рази — безкоштовно.",
        TiltIntroOn = "🧊 Увімкнути",
        TiltIntroOff = "Не треба",
        TiltIntroEnabled = "✅ Увімкнено. Напишу, коли почнеться серія. Безкоштовних сигналів: {0}.",
        TiltIntroEnabledPlus = "✅ Увімкнено. Напишу, коли почнеться серія.",
        TiltIntroDeclined = "Гаразд, не буду. Увімкнути можна в застосунку: «Я» → «🧊 Стоп-тільт».",
        TiltTypeIce = "🧊 Лід",
        TiltTypeBoiling = "🌡 Закипаєш",
        TiltTypeVolcano = "🌋 Вулкан",
        GiftReceived = "🎁 {0} подарував тобі Clanify Плюс до {1}!\n\n🧊 Стоп-тільт уже увімкнено: напишу, коли почнеш програвати серію.",
        GiftSent = "🎁 Подарунок надіслано: {0} отримав Плюс до {1}. Дякую!",
        PlusEnding = "⏳ Твій Clanify Плюс закінчиться {0}. Продовжити — одним натисканням 👇",
        PlusRenewButton = "💎 Продовжити",
        TiltAlertHead = "🧊 Стоп-тільт: поразок поспіль — {0}.",
        TiltAlertStats = "За твоїми ж боями після двох поразок поспіль ти виграєш {0}%, а зазвичай — {1}%.",
        TiltAlertGeneric = "Після серії поразок легко загратися й програти ще — дай голові охолонути.",
        TiltAlertTail = "Пауза 10–15 хвилин: вода, пара хвилин без телефона — і назад з холодною головою.\n\nВимкнути: «Я» → «⚔️ Розбір боїв».",
        LinkedHead = "✅ Прив’язав: {0}",
        LinkedStats = "⚔️ Останні {0} боїв: {1}% перемог ({2}–{3})",
        LinkedTough = "🎯 Найважче проти «{0}»: {1}% перемог у {2} боях",
        LinkedTilt = "🧊 Після двох поразок поспіль: {0}% перемог",
        LinkedTrial = "🎁 Увімкнув тобі Плюс на {0} дн. безкоштовно: повний розбір і «Стоп-тільт» — напишу, коли час зробити паузу.",
        LinkedNoBattles = "Боїв 1 на 1 у журналі поки немає — зіграй кілька боїв, і я почну розбір.",
        LinkedTail = "Повний розбір — у застосунку: проти чого програєш, коли граєш краще, мета топу 👇",
        LinkedNoClanLine = "Ти не в клані — війна недоступна, але розбір боїв працює і без клану.",
        OpenAppButton = "🎮 Відкрити застосунок",
        OpenReviewButton = "📊 Відкрити розбір",
        ForeignTagHead = "👤 {0} · {1}",
        ForeignTagLinkedAs = "Ти прив’язаний як {0}. Якщо це твій другий акаунт — натисни кнопку, і прив’язка переїде на нього. Подивитися гравця детальніше можна в застосунку → «Пошук».",
        ForeignTagRelinkButton = "🔗 Це мій акаунт",
        RelinkDone = "✅ Тепер ти прив’язаний як {0}.",
        RelinkFailed = "Не вдалося перепривʼязати — надішли тег ще раз.",
        HelpText = "📖 Що я вмію\n\nНадішли свій тег (наприклад #2VUPLPU0R) — прив’яжу й розберу твої бої. Клан не потрібен.\n\n/me — короткий розбір твоїх боїв\n/deck — твоя колода з останнього бою\n/meta — найкращі колоди топу за тиждень\n/tracker — трекер боїв: розбір після кожного бою\n/plus — Clanify Плюс\n/paysupport — допомога з оплатою\n/terms — умови\n\nУ групі клану: /setup #ТЕГ_КЛАНУ — підключити війну, /status — хто не дограв.",
        NotLinkedYet = "Спершу надішли свій тег — наприклад #2VUPLPU0R.",
        DeckHead = "🃏 Твоя колода з останнього бою (💧{0}):\n{1}",
        DeckNone = "Не знайшов боїв 1 на 1 у журналі — зіграй бій, і я покажу колоду.",
        DeckOpenButton = "🃏 Відкрити в грі",
        MetaHead = "🔥 Найкращі колоди топу за тиждень (боїв: {0})",
        MetaRow = "{0}. {1}% перемог · {2} ігор\n{3}",
        MetaEmpty = "Мета топу ще збирається — зазирни завтра.",
        PlusInfo = "💎 Clanify Плюс\n\n🧊 Стоп-тільт: напишу «стоп» просто під час гри, коли почнеш програвати серію, — з твоїми ж цифрами, кнопкою паузи й підсумком заходу.\n🛑 Свої правила: після 2 або 3 поразок, ліміт на вечір, тихі години.\n🔬 І ще: повний розбір боїв і контри до твоїх колод за боями топу.\n\n{0}",
        PlusActiveLine = "✅ Плюс активний до {0}.",
        PlusOfferLine = "7 днів — {0}⭐ · 30 днів — {1}⭐. Разова перепустка, без автопродовження. Купити — у застосунку 👇",
        PlusFreeLine = "Зараз усе відкрито безкоштовно — користуйся 🙂",
        PlusButton = "💎 Відкрити Плюс",
        PaySupport = "💬 Допомога з оплатою\n\nЗірки списалися, а Плюс чи спонсорство не увімкнулися? Хочеш повернути зірки? Напиши {0} і перешли повідомлення про оплату — у ньому номер платежу. За ним усе увімкнуть вручну або повернуть зірки.",
        PaySupportOwnerFallback = "власнику бота",
        Terms = "📄 Умови\n\nClanify — неофіційний фан-проєкт, не пов’язаний із Supercell і не схвалений нею. Дані — з офіційного Clash Royale API.\n\nПлюс і спонсорство — разові цифрові перепустки на вказаний строк, без автопродовження. Оплата — зірками Telegram. Передумав — повернемо зірки без питань протягом 48 годин після оплати: /paysupport. Якщо щось не працює — теж туди, увімкнемо вручну.\n\nМи зберігаємо твій тег, бої за 30 днів і налаштування сповіщень — лише для роботи бота.",
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
        TrkCardHead = "🎯 Захід з {0} · {1}–{2} · {3}🏆",
        TrkLast = "{0} {1}–{2} · {3}🏆 · {4} · {5}",
        TrkVAfk = "💡 Схоже на виліт або АФК: витекло {0} еліксиру.",
        TrkVLevels = "💡 Суперник прокачаний сильніше: +{0} рівня в середньому. Найнижча в тебе «{1}» ({2}).",
        TrkVClose = "💡 Близько: у його вежі лишалося {0} HP.",
        TrkVLeak = "💡 Витекло {0} еліксиру — зазвичай у тебе {1}.",
        TrkVEven = "💡 Рівні й колоди на рівних — вирішилося в самому бою. Повтор є в журналі боїв у грі.",
        TrkVWinLevels = "💡 Перемога з рівнями {0} — сильно.",
        TrkVWinUpset = "💡 Обіграв суперника на {0}🏆 вище.",
        TrkVWinRank = "💡 Обіграв №{0} у світі.",
        TrkVWinClose = "💡 Витягнув: у твоєї вежі лишалося {0} HP.",
        TrkPVsArch = "Проти {0} за місяць {1}–{2} (зазвичай ти {3}%)",
        TrkPBetterDeck = "Проти {0} краще йде твоя колода з «{1}»: {2}–{3}",
        TrkTiltPause = "🧊 Пауза до {0}",
        TrkAlertPrefix = "Бій {0}: {1}–{2} проти {3} · {4}🏆",
        TrkSumHead = "🏁 Захід {0}–{1} · {2}–{3} · {4}🏆",
        TrkSumBest = "⭐ Найкращий: {0}–{1} проти {2} ({3}🏆)",
        TrkSumWorst = "😖 Найважче: {0} — {1}–{2} за вечір",
        TrkSumLevels = "Рівні: у поразках {0}, у перемогах {1}",
        TrkSumMoment = "🧊 Стоп-тільт зупинив би тебе після {0}-го бою — далі було {1}–{2}, {3}🏆",
        TrkSumLocked = "🔒 За захід {0} підказки «проти кого й чим грати» — у Плюсі",
        TrkMuted = "🔕 Сьогодні без карток. Бої все одно зберігаються в історію.",
        TrkBelow = "↓ Картка заходу нижче",
        TrkOn = "🎯 Трекер боїв увімкнено. Після кожного бою я тихо оновлюю одну картку заходу — без звуку. Розбір кожного бою й історія — у застосунку. Вимкнути — /tracker.",
        TrkOff = "Трекер вимкнено. Історія боїв у застосунку лишається.",
        TrkStatus = "🎯 Трекер боїв: {0}\n\nПісля кожного бою — тиха картка заходу: рахунок, суперник, що вирішило бій. Усі бої з розбором — у застосунку.",
        TrkStatusOn = "увімкнено ✅",
        TrkStatusOff = "вимкнено",
        TrkUnavailable = "🎯 Трекер боїв поки в закритому тесті — скоро відкриємо всім.",
        TrkOther = "колода з «{0}»",
        TrkBtnReport = "📖 Розбір",
        TrkBtnAll = "📜 Усі бої",
        TrkBtnMute = "🔕 Сьогодні без карток",
        TrkBtnSession = "📜 Розбір заходу",
        TrkBtnPlus = "⭐ Плюс",
        TrkBtnOn = "Увімкнути",
        TrkBtnOff = "Вимкнути",
        TrkInGroup = "🎯 Трекер боїв працює в особистих: там я після кожного бою тихо надсилаю розбір. Натисни кнопку нижче — і увімкни в один тап.",
        TrkBtnDm = "🎯 Увімкнути в особистих",
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
            "🏆 {0} — 900 in a day. Four battles, four bodies, no witnesses",
            "👑 900/900 for {0}. The opponents changed their names and joined another clan 📝",
            "🚀 {0} closed the day at 900. Somewhere at Supercell they're rechecking the card balance",
            "💪 {0}: 900 out of 900. Even the tower didn't understand what hit it",
            "⚡ 900 in a day from {0}. The opponents are still looking for the surrender button",
            "🔥 {0} had a perfect day. Today he played — everyone else merely attended",
            "🎯 900 for {0}. Three crowns are officially his signature now ✍️",
            "🧊 {0} — 900 in a day without blinking. Ice-cold, and someone else's tears",
            "📈 {0} scored 900. The clan grows, the rivals go to therapy",
            "🛡️ 900/900 from {0}. There was a defense. It just wasn't needed",
            "🤖 {0} maxed out 4/4. Suspiciously clean. Someone check if he's human",
            "🍿 {0} — 900 in a day. The rest could have skipped playing and just watched",
        ],

        RespectTitle = "👏 <b>Respects of the day</b>",
        RespectFooter = "<i>Today's total: {0}. One respect per day — open the app to give yours.</i>",

        SmartAlert = "📉 Without your attacks the clan's win chance drops from {0}% to {1}%!\n"
                   + "Decks left: {2}/4 — get them in.",


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

        StartPrivate = "⚔️ Clanify — your Clash Royale sidekick\n\nSend your tag — e.g. #2VUPLPU0R. No clan needed.\n\nWhat I'll do:\n• remember your battles and show which cards you struggle against\n• measure your tilt, elixir leak and best time to play\n• show what the world's top 500 are winning with right now\n\nAnd if you're in a clan — I'll remind you about war decks and track everyone's contribution 🏰\n\nAll commands: /help",
        GroupJoined = "👋 Hi! I'm Clanify, a bot for Clash Royale clan wars.\n\n"
                    + "I open as an app right inside Telegram, nothing to install.\n\n"
                    + "Two steps left:\n\n"
                    + "1️⃣ A GROUP ADMIN types here:\n"
                    + "/setup #CLAN_TAG\n"
                    + "The tag is in the game, on the clan profile.\n\n"
                    + "2️⃣ EVERY PLAYER taps the button below and links themselves.\n\n"
                    + "After that I show who played the war and who didn't, and remind those who forgot.",
        GroupJoinedReady = "👋 Back again! Clan \"{0}\" is already linked to this group.\n\n"
                         + "Anyone not linked yet, tap the button below.",
        StartGroupNew = "⚔️ Clanify — Clash Royale war stats\n\n"
                      + "1️⃣ Group admin: /setup #CLAN_TAG\n"
                      + "2️⃣ Players: the button below\n\n"
                      + "The clan tag is in the game, on the clan profile.",
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
        SetupOk = "✅ Clan \"{0}\" is connected!\n\n"
                + "Now every player taps the button below and links themselves.\n"
                + "It takes half a minute and is done once.\n\n"
                + "Anyone not linked is invisible to me: no stats, no war reminders.",
        SetupTopicNote = "\n\n📌 Reminders and reports will arrive in this topic.",
        LinkFormat = "Format: /link #YOURTAG",
        LinkNotFound = "❌ Player not found. Check the tag (profile → the tag under your name).",
        LinkOkPrivate = "✅ Linked player \"{0}\"! Open the Mini App from the menu button.",
        LinkOkGroup = "✅ Linked player \"{0}\".\n\n"
                    + "To get reminders in private, open the app with the button below.",
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
        ErrDb = "⚠️ Something broke on our side. The error is logged, try again in a minute.",
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
        TiltHead = "🧊 Stop-tilt · {0} losses in a row{1}",
        TiltCoin = " That's worse than a coin flip 🪙",
        TiltSameCard = "Every time the opponent had «{0}».",
        TiltTips = "A 10–15 minute break — and your odds come back.|Breathe, grab some water and come back with a clear head.|Even the top 1000 take breaks after two losses.|A streak isn't a verdict. A break is part of the game too.|Your trophies won't go anywhere in 15 minutes.",
        TiltFreeLeft = "Free signals left: {0}.",
        TiltFreeLast = "That was the last free signal — next ones come with Plus.",
        TiltBtnPause = "⏸ Break 15 min",
        TiltBtnGo = "▶️ Keep playing",
        TiltBtnMute = "🔕 Not today",
        TiltPausedLine = "⏸ Break until {0}. I'll tell you when.",
        TiltGoLine = "▶️ Got it, quiet until this session ends.",
        TiltMutedLine = "🔕 No more messages today.",
        TiltResume = "🟢 Go ahead. Start with your «{0}» deck — {1}% wins this month.",
        TiltResumeGeneric = "🟢 Break's over — you can play. Good luck!",
        TiltSummaryHead = "🧊 Stop-tilt · session summary",
        TiltSummaryScore = "{0}–{1}{2}",
        TiltSummaryPause = "⏸ Break → after it {0}–{1}",
        TiltSummaryAfter = "After the signal: {0}–{1}",
        TiltSummaryStopped = "✅ You stopped after the signal — trophies saved.",
        TiltSummaryWorks = "After a break you win {0}%, without one — {1}%.",
        TiltLimitHead = "🛑 Evening limit: {0} losses today.",
        TiltLimitBody = "You set this limit yourself. Maybe that's enough for today?",
        TiltIntro = "🧊 Ready: your tilt type is {0}.\nAfter two losses in a row you win {1}%, usually {2}%.\n\nShould I message you mid-game when a losing streak starts? The first {3} are free.",
        TiltIntroOn = "🧊 Turn on",
        TiltIntroOff = "No thanks",
        TiltIntroEnabled = "✅ On. I'll message you when a streak starts. Free signals: {0}.",
        TiltIntroEnabledPlus = "✅ On. I'll message you when a streak starts.",
        TiltIntroDeclined = "OK, I won't. You can turn it on in the app: «Me» → «🧊 Stop-tilt».",
        TiltTypeIce = "🧊 Ice",
        TiltTypeBoiling = "🌡 Simmering",
        TiltTypeVolcano = "🌋 Volcano",
        GiftReceived = "🎁 {0} gifted you Clanify Plus until {1}!\n\n🧊 Stop-tilt is on: I'll message you when a losing streak starts.",
        GiftSent = "🎁 Gift sent: {0} has Plus until {1}. Thank you!",
        PlusEnding = "⏳ Your Clanify Plus ends on {0}. Renew in one tap 👇",
        PlusRenewButton = "💎 Renew",
        TiltAlertHead = "🧊 Stop-tilt: {0} losses in a row.",
        TiltAlertStats = "Your own battles say: after two losses in a row you win {0}%, usually {1}%.",
        TiltAlertGeneric = "After a losing streak it's easy to keep chasing and lose more — let your head cool down.",
        TiltAlertTail = "Take 10–15 minutes: some water, a few minutes off the phone — then come back with a clear head.\n\nTurn off: «Me» → «⚔️ Battle review».",
        LinkedHead = "✅ Linked: {0}",
        LinkedStats = "⚔️ Last {0} battles: {1}% wins ({2}–{3})",
        LinkedTough = "🎯 Hardest matchup: «{0}» — {1}% wins in {2} battles",
        LinkedTilt = "🧊 After two losses in a row: {0}% wins",
        LinkedTrial = "🎁 You get Plus free for {0} days: the full review and Stop-tilt — I'll message you when it's time for a break.",
        LinkedNoBattles = "No 1v1 battles in your log yet — play a couple and I'll start the review.",
        LinkedTail = "Full review in the app: what you lose to, when you play best, the top meta 👇",
        LinkedNoClanLine = "You're not in a clan — war isn't available, but the battle review works without one.",
        OpenAppButton = "🎮 Open the app",
        OpenReviewButton = "📊 Open the review",
        ForeignTagHead = "👤 {0} · {1}",
        ForeignTagLinkedAs = "You're linked as {0}. If this is your second account, tap the button and the link moves to it. For details on this player, use the app → «Search».",
        ForeignTagRelinkButton = "🔗 This is my account",
        RelinkDone = "✅ You're now linked as {0}.",
        RelinkFailed = "Couldn't relink — send the tag again.",
        HelpText = "📖 What I can do\n\nSend your tag (e.g. #2VUPLPU0R) — I'll link you and review your battles. No clan needed.\n\n/me — a short review of your battles\n/deck — your deck from the last battle\n/meta — the top's best decks this week\n/tracker — battle tracker: a breakdown after every battle\n/plus — Clanify Plus\n/paysupport — payment help\n/terms — terms\n\nIn a clan group: /setup #CLAN_TAG — connect the war, /status — who hasn't played.",
        NotLinkedYet = "Send your tag first — e.g. #2VUPLPU0R.",
        DeckHead = "🃏 Your deck from the last battle (💧{0}):\n{1}",
        DeckNone = "No 1v1 battles in your log — play one and I'll show the deck.",
        DeckOpenButton = "🃏 Open in game",
        MetaHead = "🔥 The top's best decks this week ({0} battles)",
        MetaRow = "{0}. {1}% wins · {2} games\n{3}",
        MetaEmpty = "The top meta is still being collected — check back tomorrow.",
        PlusInfo = "💎 Clanify Plus\n\n🧊 Stop-tilt: I'll say «stop» mid-game when a losing streak starts — with your own numbers, a break button and a session summary.\n🛑 Your rules: after 2 or 3 losses, an evening limit, quiet hours.\n🔬 Plus: the full battle review and counters to your decks from top battles.\n\n{0}",
        PlusActiveLine = "✅ Plus is active until {0}.",
        PlusOfferLine = "7 days — {0}⭐ · 30 days — {1}⭐. One-time pass, no auto-renewal. Buy it in the app 👇",
        PlusFreeLine = "Everything is free right now — enjoy 🙂",
        PlusButton = "💎 Open Plus",
        PaySupport = "💬 Payment help\n\nStars were charged but Plus or sponsorship didn't turn on? Want your stars back? Message {0} and forward the payment message — it has the payment number. We'll switch it on manually or refund the stars.",
        PaySupportOwnerFallback = "the bot owner",
        Terms = "📄 Terms\n\nClanify is an unofficial fan project, not affiliated with or endorsed by Supercell. Data comes from the official Clash Royale API.\n\nPlus and sponsorship are one-time digital passes for the stated period, with no auto-renewal. Payment is in Telegram Stars. Changed your mind? We refund the stars, no questions asked, within 48 hours of payment: /paysupport. If something doesn't work — same place, we'll switch it on manually.\n\nWe store your tag, 30 days of battles and notification settings — only to run the bot.",
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
        TrkCardHead = "🎯 Session since {0} · {1}–{2} · {3}🏆",
        TrkLast = "{0} {1}–{2} · {3}🏆 · {4} · {5}",
        TrkVAfk = "💡 Looks like a disconnect or AFK: {0} elixir leaked.",
        TrkVLevels = "💡 Opponent's cards were higher: +{0} levels on average. Your lowest: «{1}» ({2}).",
        TrkVClose = "💡 Close one: their tower had {0} HP left.",
        TrkVLeak = "💡 {0} elixir leaked — you usually leak {1}.",
        TrkVEven = "💡 Levels and decks were even — it was decided in the battle itself. The replay is in your in-game battle log.",
        TrkVWinLevels = "💡 Won while {0} levels down — strong.",
        TrkVWinUpset = "💡 Beat an opponent {0}🏆 above you.",
        TrkVWinRank = "💡 Beat world #{0}.",
        TrkVWinClose = "💡 Clutch: your tower had {0} HP left.",
        TrkPVsArch = "Vs {0} this month {1}–{2} (you usually win {3}%)",
        TrkPBetterDeck = "Vs {0} your «{1}» deck does better: {2}–{3}",
        TrkTiltPause = "🧊 Break until {0}",
        TrkAlertPrefix = "Battle {0}: {1}–{2} vs {3} · {4}🏆",
        TrkSumHead = "🏁 Session {0}–{1} · {2}–{3} · {4}🏆",
        TrkSumBest = "⭐ Best: {0}–{1} vs {2} ({3}🏆)",
        TrkSumWorst = "😖 Toughest: {0} — {1}–{2} tonight",
        TrkSumLevels = "Levels: {0} in losses, {1} in wins",
        TrkSumMoment = "🧊 Stop-tilt would have stopped you after battle {0} — then it went {1}–{2}, {3}🏆",
        TrkSumLocked = "🔒 {0} «who you lose to and what to play» tips this session — in Plus",
        TrkMuted = "🔕 No cards today. Battles are still saved to your history.",
        TrkBelow = "↓ Session card is below",
        TrkOn = "🎯 Battle tracker is on. After each battle I quietly update one session card — no sound. Every battle's breakdown and your history are in the app. Turn off — /tracker.",
        TrkOff = "Tracker is off. Your battle history stays in the app.",
        TrkStatus = "🎯 Battle tracker: {0}\n\nAfter each battle — a silent session card: score, opponent, what decided it. Every battle with a breakdown is in the app.",
        TrkStatusOn = "on ✅",
        TrkStatusOff = "off",
        TrkUnavailable = "🎯 The battle tracker is in closed testing — opening to everyone soon.",
        TrkOther = "«{0}» deck",
        TrkBtnReport = "📖 Breakdown",
        TrkBtnAll = "📜 All battles",
        TrkBtnMute = "🔕 No cards today",
        TrkBtnSession = "📜 Session breakdown",
        TrkBtnPlus = "⭐ Plus",
        TrkBtnOn = "Turn on",
        TrkBtnOff = "Turn off",
        TrkInGroup = "🎯 The battle tracker works in private chat: that's where I quietly send a breakdown after every battle. Tap the button below to turn it on.",
        TrkBtnDm = "🎯 Turn on in private chat",
    };
}
