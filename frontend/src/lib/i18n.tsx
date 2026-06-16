import { createContext, useContext, useState, type ReactNode } from 'react'

export type Lang = 'ru' | 'uk' | 'en'

const ru = {
  loading: 'Загрузка',
  loadingWar: 'Загружаю статус войны…',
  openViaTitle: 'Открой приложение через Telegram',
  openViaHint: 'Зайди в группу клана или в чат с ботом и открой Mini App кнопкой — при открытии по обычной ссылке Telegram не передаёт данные для входа.',
  serverError: 'Ошибка сервера — проверьте логи API или попробуйте позже',
  networkError: 'Ошибка сети',
  retry: 'Повторить',
  dateLocale: 'ru-RU',

  tabs: { war: 'Война', rating: 'Рейтинг', me: 'Я', search: 'Поиск', owner: 'Панель', recruit: 'Биржа' },

  period: { training: 'Тренировочные дни', warDay: 'День войны', colosseum: 'Колизей' },
  header: { dayOf4: '· день', untilEnd: 'До конца дня:', h: 'ч', played: 'сыграли' },

  stats: { weekMedals: 'медали недели', decksToday: 'колод сегодня', medalsPerBattle: 'медалей/бой', active: 'активных' },

  race: {
    title: '⛵ Гонка недели',
    trainingNote: 'медали с четверга',
    ours: 'мы',
    todayTitle: 'Медали за сегодня',
    boatTitle: 'Очки лодки за сегодня',
    totalTitle: 'Всего медалей за неделю',
    projTitle: 'Прогноз медалей к концу дня (avg × 200)',
  },

  forecast: {
    titleSimple: '🔮 Прогноз',
    title: '🔮 Прогноз клана',
    trainingNote: 'Идут тренировочные дни — прогноз появится с началом войны.',
    lockedNote: 'Сколько медалей клан наберёт за сегодня и за неделю, тренд и точность прогноза — доступно на тарифе Pro. 🔒',
    trend: { ahead: 'Идём выше темпа', onPace: 'Идём в графике', behind: 'Отстаём от темпа' },
    dayLabel: '🏅 очков за сегодняшний день',
    range: 'диапазон:',
    weekLabel: '🏆 к концу недели:',
    attacksLeft: 'атак осталось: ~',
    accuracy: 'Точность прогноза',
    howTitle: 'Как считается прогноз?',
    howDay1: 'За сегодняшний день: набрано сегодня + ',
    howDay2: ' оставшихся атак клана × средние медали клана за атаку. Считаем очки только текущего военного дня, а не сумму за всю неделю.',
    howWeek: 'К концу недели: то же, но до конца всех 4 военных дней. «Диапазон» — это разброс ±1σ (как может лечь реальный результат). Чем больше сыграно дней и чем активнее клан — тем выше точность.',
  },

  players: { empty: 'Нет участников войны — возможно, идёт тренировка.', noTg: 'нет TG', decksDots: 'из 4 колод' },

  roles: { leader: 'Лидер', coleader: 'Соруководитель', elder: 'Старейшина' },

  insights: {
    title: '🧠 Аналитика клана',
    lockedNote: 'Шанс победы в гонке в реальном времени, «здоровье» клана и DNA-профили игроков (кто тащит, а кто балласт) — доступно на тарифе Pro. 🔒',
    winCaption: 'шанс победы в гонке',
    topRival: 'главный соперник:',
    slackersWarn1: 'Если не доигравшие так и не сыграют — шанс упадёт до ',
    slackersWarn2: '%',
  },

  warlog: {
    title: '📜 Журнал войн',
    hint: 'тапни неделю — все кланы',
    victory: '🥇 победа',
    placePrefix: '',
    placeSuffix: '-е место',
    season: 'сезон',
    ours: 'мы',
    close: 'Закрыть',
    loading: 'Загружаю журнал…',
    error: 'Журнал недоступен.',
    empty: 'У клана пока нет завершённых войн в журнале.',
    pastWars: '📜 Прошлые войны',
  },

  history: {
    title: '📈 История войн',
    lockedNote: 'Динамика клана по неделям и дням войны — доступно на тарифе Pro. 🔒',
    emptyNote: 'История копится автоматически с каждым днём войны — загляни сюда после первого военного дня.',
    currentWeek: 'Текущая неделя',
    colosseum: '· Колизей',
    day: 'День',
    leaders: '👑 Лидеры:',
    myContrib: 'Твой вклад на этой неделе:',
  },

  search: {
    title: '🔍 Поиск игрока',
    hint: 'Введи тег игрока (например, #ABC123) — покажем полный профиль.',
    placeholder: '#ABC123',
    find: 'Найти',
    finding: '…',
    notFound: 'Игрок не найден',
    royaleApi: 'RoyaleAPI ↗',
    level: 'Уровень',
    trophies: 'Кубки',
    warTrophies: 'КВ кубки',
    arena: 'Арена',
    warStats: '⚔️ Статистика войн (из журнала клана)',
    weeks: 'Недель',
    totalMedals: 'Медали всего',
    perAttack: 'За атаку',
    cards: 'Карты',
    tabProfile: 'Профиль',
    tabWars: 'Войны КВ',
    lvl: 'lvl',
  },

  me: {
    notInWar: 'Тебя нет в составе текущей войны',
    loadError: 'Не удалось загрузить статистику',
    contrib: 'вклад в клан',
    contribAria: 'Вклад',
    rankOf: 'место',
    of: 'из',
    weekMedals: '🏅 медали за неделю',
    decksToday: '🃏 колоды сегодня',
    medalsPerAttack: 'медали / атака',
    vsClan: '% от среднего по клану',
    weekAttacks: 'атак за неделю',
    dayForecast: '🔮 прогноз на день',
    weekForecast: '🔮 прогноз недели',
    seasonTitle: '🗓 Сезон #',
    seasonWeeks: 'недель:',
    seasonMedals: '🏅 медали за сезон',
    seasonRankSuffix: 'в зачёте',
    bestWeek: '⚡ лучшая неделя',
    weeksIn: 'недель участвовал',
    share: '📤 Поделиться результатами',
    shareHeader: '⚔️ Мои результаты в Clan War',
    shareFame: '🏅 Слава:',
    shareAvg: '⚡',
    shareWeekForecast: '🔮 Прогноз недели:',
    shareSeason: '🗓 За сезон:',
    shareContrib: 'Мой вклад:',
    shareContribSuffix: '% медалей клана 💪',
    medalsPerAttackUnit: 'медалей за атаку',
    perf: { top: 'топ', above: 'выше среднего', mid: 'средне', below: 'ниже среднего' },
  },

  leaderboard: {
    title: '🏆 Рейтинг',
    period: 'Период рейтинга',
    current: '📅 Текущая война',
    season: '🏆 Весь сезон',
    global: '🌍 Топ бота',
    noDataCurrent: 'Пока нет данных для рейтинга.',
    mvpWeek: '👑 MVP недели:',
    mvpSeason: '👑 MVP сезона:',
    mvpBot: '👑 Чемпион бота:',
    medals: 'медалей',
    you: 'ты',
    weeks: 'нед',
    perAttack: '/атака',
    globalEmpty: 'Здесь соревнуются игроки из всех кланов бота, привязавшие аккаунт через /link. Данные копятся с каждой неделей войны.',
    globalMeta: 'Игроки всех кланов бота · окно:',
    globalMetaWeeks: 'недель · участников:',
    seasonEmpty: 'Данные сезона появятся после первого военного дня.',
    seasonMeta: 'Сезон #',
    seasonMetaWeeks: '· недель в зачёте:',
    seasonMetaSuffix: '(W1 → текущая)',
    best: 'луч.',
    streakTitle: 'недель подряд',
  },

  about: {
    title: 'ℹ️ Как это работает',
    intro: 'Бот следит за Клановой Войной: 4 военных дня, у каждого игрока 4 колоды в день. Все цифры берутся из официального Clash Royale API, прогнозы — по истории прошлых войн.',
    upsell: '🔒 Пункты с меткой Pro открываются на тарифе Pro. За подключением — к владельцу бота.',
    features: [
      { icon: '⚔️', title: 'Статус войны', desc: 'Кто сыграл 4/4, кто не доиграл, сколько времени до конца военного дня.', pro: false },
      { icon: '🏁', title: 'Гонка кланов', desc: 'Места и очки всех кланов недели, прогноз финала. Тап по клану — его прошлые войны.', pro: false },
      { icon: '🏆', title: 'Рейтинг недели и топ бота', desc: 'Кто больше всех набил за текущую неделю + глобальный топ игроков всех кланов.', pro: false },
      { icon: '🔮', title: 'Прогноз клана', desc: 'Сколько медалей наберёте за сегодня и к концу недели, с диапазоном и точностью.', pro: false },
      { icon: '📅', title: 'Прошлые войны и сезон', desc: 'Рейтинг игроков за каждую войну (W1, W2…) и суммарный зачёт за весь сезон.', pro: false },
      { icon: '📜', title: 'История по неделям', desc: 'Графики и динамика клана и лично твоя за прошлые недели войн.', pro: false },
      { icon: '👉', title: '«Пнуть» не сыгравших', desc: 'Разослать напоминание всем сразу одной кнопкой (Free — до 20 человек).', pro: false },
      { icon: '⏰', title: 'Автонапоминания', desc: 'Личное сообщение каждому не доигравшему за N часов до конца дня.', pro: true },
      { icon: '📊', title: 'Pro-аналитика', desc: 'Шанс победы в гонке, здоровье клана и архетип каждого игрока (Тащер, Надёжный…).', pro: true },
    ],
  },

  playerModal: {
    close: 'Закрыть',
    rankInClan: 'в клане',
    noTg: 'нет TG',
    reliability: 'надёжность',
    weekMedals: '🏅 медали за неделю',
    avgAttack: '⚡ в среднем за атаку',
    totalAttacks: '⚔️ атак всего',
    boatAttacks: '🚤 атаки лодки',
    dayForecast: '🔮 прогноз на день',
    weekForecast: '🔮 прогноз недели',
    repairPoints: '🔧 Очки ремонта:',
    forecastNote: '🔮 Прогноз = текущие медали + ожидаемые оставшиеся атаки, умноженные на средние медали за атаку этого игрока (по его прошлым войнам). Чем больше истории — тем точнее.',
    pastWars: '📜 Прошлые войны',
    loadingHistory: 'Загружаю историю…',
    historyError: 'История недоступна.',
    noHistory: 'Пока нет данных — история копится с момента подключения клана к боту.',
    royaleApi: 'Полный профиль на RoyaleAPI ↗',
    decksToday: 'колоды сегодня:',
    played: 'Сыграл все колоды',
    timeLeft: 'Ещё есть время',
    notPlayed: 'Не доиграл — дедлайн близко',
    season: 'сезон',
  },

  nudge: {
    sending: 'Пинаю…',
    label: 'Пнуть лентяев',
    freeLimit: ' · лимит 20 на Free',
    done: 'Пнул!',
    allPlayed: 'Все уже сыграли 🎉',
    noWar: 'Сейчас не день войны',
    error: 'Не получилось — попробуй позже',
    dm: '✉️ в ЛС:',
    chat: '💬 в чат:',
    skipped: '⏸ недавно пинали:',
  },

  reminder: {
    title: '⏰ Автонапоминания',
    saved: '✓ сохранено',
    desc1: 'Бот пришлёт личное сообщение в Telegram тем, кто не отыграл все 4/4 колоды, за ',
    desc2: ' ч до конца военного дня (день заканчивается в 10:00 UTC). В групповой чат уходит общая сводка.',
    dmTitle: '💬 Личные напоминания:',
    dmFree: 'Free',
    dmPro: 'Pro',
    dmFreeDesc: '— до',
    dmFreeDescSuffix: 'игроков,',
    dmProDesc: '— всем привязанным без лимита.',
    upsell1: '🔒 Сейчас личные напоминания идут',
    upsell2: 'из',
    upsell3: 'привязанных игроков.',
    h: 'ч',
  },

  link: {
    title: 'Аккаунт не привязан',
    desc: 'Отправь боту в групповом чате клана команду:',
    hint: 'Свой тег найдёшь в Clash Royale: профиль → значок под именем.',
  },

  recruit: {
    tabLabel: 'Биржа',
    myTitle: '🔍 Ищу клан',
    myHint: 'Твоя анкета будет видна лидерам кланов с тарифом Pro.',
    noteLabel: 'О себе (необязательно)',
    notePlaceholder: 'Активность, опыт, пожелания…',
    activate: 'Разместить анкету',
    deactivate: 'Снять анкету',
    activeStatus: '✅ Анкета активна — лидеры кланов тебя видят',
    inactiveStatus: 'Анкета не активна',
    saving: '…',
    boardTitle: '👥 Биржа игроков',
    boardHint: 'Игроки, ищущие клан. Тап на карточку — открыть в Telegram.',
    empty: 'Пока никто не ищет клан 🤷',
    loading: 'Загружаю кандидатов…',
    error: 'Не удалось загрузить список',
    weeks: 'нед',
    medalsTotal: 'медалей',
    perAttack: 'за атаку',
    openTg: 'Написать ↗',
    ago: 'подал заявку',
  },

  owner: {
    title: '⚙️ Панель владельца',
    clans: 'Кланов:',
    pro: 'Pro:',
    linked: 'TG-привязок:',
    expiry: 'до',
    pro30: 'Pro 30д',
    pro90: 'Pro 90д',
    proInf: 'Pro ∞',
    free: 'Free',
    delete: '🗑 Удалить',
    confirmDelete: '❗ Точно удалить?',
    deleteHint: 'Удалит клан, все TG-привязки игроков и историю войн. Нажми ещё раз для подтверждения.',
    noClans: 'Пока ни одного клана — кланы появляются после /setup в группе.',
    error: 'Нет доступа или ошибка сети.',
  },
}

export type Translations = typeof ru

const uk: Translations = {
  loading: 'Завантаження',
  loadingWar: 'Завантажую статус війни…',
  openViaTitle: 'Відкрий застосунок через Telegram',
  openViaHint: 'Зайди у групу клану або в чат з ботом і відкрий Mini App кнопкою — при відкритті за звичайним посиланням Telegram не передає дані для входу.',
  serverError: 'Помилка сервера — перевірте логи API або спробуйте пізніше',
  networkError: 'Помилка мережі',
  retry: 'Повторити',
  dateLocale: 'uk-UA',

  tabs: { war: 'Війна', rating: 'Рейтинг', me: 'Я', search: 'Пошук', owner: 'Панель', recruit: 'Біржа' },

  period: { training: 'Тренувальні дні', warDay: 'День війни', colosseum: 'Колізей' },
  header: { dayOf4: '· день', untilEnd: 'До кінця дня:', h: 'год', played: 'зіграли' },

  stats: { weekMedals: 'медалі тижня', decksToday: 'колод сьогодні', medalsPerBattle: 'медалей/бій', active: 'активних' },

  race: {
    title: '⛵ Гонка тижня',
    trainingNote: 'медалі з четверга',
    ours: 'ми',
    todayTitle: 'Медалі за сьогодні',
    boatTitle: 'Очки човника за сьогодні',
    totalTitle: 'Усього медалей за тиждень',
    projTitle: 'Прогноз медалей до кінця дня (avg × 200)',
  },

  forecast: {
    titleSimple: '🔮 Прогноз',
    title: '🔮 Прогноз клану',
    trainingNote: "Йдуть тренувальні дні — прогноз з'явиться з початком війни.",
    lockedNote: 'Скільки медалей клан набере за сьогодні та за тиждень, тренд і точність прогнозу — доступно на тарифі Pro. 🔒',
    trend: { ahead: 'Ідемо вище темпу', onPace: 'Ідемо в графіку', behind: 'Відстаємо від темпу' },
    dayLabel: '🏅 очок за сьогоднішній день',
    range: 'діапазон:',
    weekLabel: '🏆 до кінця тижня:',
    attacksLeft: 'атак залишилось: ~',
    accuracy: 'Точність прогнозу',
    howTitle: 'Як рахується прогноз?',
    howDay1: 'За сьогоднішній день: набрано сьогодні + ',
    howDay2: ' атак клану, що залишились × середні медалі клану за атаку. Рахуємо очки тільки поточного воєнного дня, а не суму за весь тиждень.',
    howWeek: "До кінця тижня: те ж саме, але до кінця всіх 4 воєнних днів. «Діапазон» — це розкид ±1σ (як може лягти реальний результат). Чим більше зіграно днів і чим активніший клан — тим вища точність.",
  },

  players: { empty: 'Немає учасників війни — можливо, йде тренування.', noTg: 'нема TG', decksDots: 'з 4 колод' },

  roles: { leader: 'Лідер', coleader: 'Заступник', elder: 'Старійшина' },

  insights: {
    title: '🧠 Аналітика клану',
    lockedNote: "Шанс перемоги в гонці в реальному часі, «здоров'я» клану та DNA-профілі гравців (хто тягне, а хто баласт) — доступно на тарифі Pro. 🔒",
    winCaption: 'шанс перемоги в гонці',
    topRival: 'головний суперник:',
    slackersWarn1: 'Якщо ті, хто не дограв, так і не зіграють — шанс впаде до ',
    slackersWarn2: '%',
  },

  warlog: {
    title: '📜 Журнал воєн',
    hint: 'тапни тиждень — всі клани',
    victory: '🥇 перемога',
    placePrefix: '',
    placeSuffix: '-е місце',
    season: 'сезон',
    ours: 'ми',
    close: 'Закрити',
    loading: 'Завантажую журнал…',
    error: 'Журнал недоступний.',
    empty: 'У клану поки немає завершених воєн у журналі.',
    pastWars: '📜 Минулі війни',
  },

  history: {
    title: '📈 Історія воєн',
    lockedNote: 'Динаміка клану за тижнями та днями війни — доступно на тарифі Pro. 🔒',
    emptyNote: 'Історія накопичується автоматично з кожним днем війни — загляни сюди після першого воєнного дня.',
    currentWeek: 'Поточний тиждень',
    colosseum: '· Колізей',
    day: 'День',
    leaders: '👑 Лідери:',
    myContrib: 'Твій вклад на цьому тижні:',
  },

  search: {
    title: '🔍 Пошук гравця',
    hint: 'Введи тег гравця (наприклад, #ABC123) — покажемо повний профіль.',
    placeholder: '#ABC123',
    find: 'Знайти',
    finding: '…',
    notFound: 'Гравця не знайдено',
    royaleApi: 'RoyaleAPI ↗',
    level: 'Рівень',
    trophies: 'Кубки',
    warTrophies: 'КВ кубки',
    arena: 'Арена',
    warStats: '⚔️ Статистика воєн (з журналу клану)',
    weeks: 'Тижнів',
    totalMedals: 'Медалей загалом',
    perAttack: 'За атаку',
    cards: 'Карти',
    tabProfile: 'Профіль',
    tabWars: 'Війни КВ',
    lvl: 'lvl',
  },

  me: {
    notInWar: 'Тебе немає у складі поточної війни',
    loadError: 'Не вдалося завантажити статистику',
    contrib: 'вклад у клан',
    contribAria: 'Вклад',
    rankOf: 'місце',
    of: 'з',
    weekMedals: '🏅 медалі за тиждень',
    decksToday: '🃏 колоди сьогодні',
    medalsPerAttack: 'медалі / атака',
    vsClan: '% від середнього по клану',
    weekAttacks: 'атак за тиждень',
    dayForecast: '🔮 прогноз на день',
    weekForecast: '🔮 прогноз тижня',
    seasonTitle: '🗓 Сезон #',
    seasonWeeks: 'тижнів:',
    seasonMedals: '🏅 медалі за сезон',
    seasonRankSuffix: 'у заліку',
    bestWeek: '⚡ кращий тиждень',
    weeksIn: 'тижнів брав участь',
    share: '📤 Поділитися результатами',
    shareHeader: '⚔️ Мої результати в Clan War',
    shareFame: '🏅 Слава:',
    shareAvg: '⚡',
    shareWeekForecast: '🔮 Прогноз тижня:',
    shareSeason: '🗓 За сезон:',
    shareContrib: 'Мій вклад:',
    shareContribSuffix: '% медалей клану 💪',
    medalsPerAttackUnit: 'медалей за атаку',
    perf: { top: 'топ', above: 'вище середнього', mid: 'середньо', below: 'нижче середнього' },
  },

  leaderboard: {
    title: '🏆 Рейтинг',
    period: 'Період рейтингу',
    current: '📅 Поточна війна',
    season: '🏆 Весь сезон',
    global: '🌍 Топ бота',
    noDataCurrent: 'Поки немає даних для рейтингу.',
    mvpWeek: '👑 MVP тижня:',
    mvpSeason: '👑 MVP сезону:',
    mvpBot: '👑 Чемпіон бота:',
    medals: 'медалей',
    you: 'ти',
    weeks: 'тиж',
    perAttack: '/атака',
    globalEmpty: "Тут змагаються гравці з усіх кланів бота, що прив'язали акаунт через /link. Дані накопичуються з кожним тижнем війни.",
    globalMeta: 'Гравці всіх кланів бота · вікно:',
    globalMetaWeeks: 'тижнів · учасників:',
    seasonEmpty: "Дані сезону з'являться після першого воєнного дня.",
    seasonMeta: 'Сезон #',
    seasonMetaWeeks: '· тижнів у заліку:',
    seasonMetaSuffix: '(W1 → поточна)',
    best: 'кращ.',
    streakTitle: 'тижнів поспіль',
  },

  about: {
    title: 'ℹ️ Як це працює',
    intro: 'Бот стежить за Клановою Війною: 4 воєнних дні, у кожного гравця 4 колоди на день. Всі цифри беруться з офіційного Clash Royale API, прогнози — за історією минулих воєн.',
    upsell: '🔒 Пункти з міткою Pro відкриваються на тарифі Pro. За підключенням — до власника бота.',
    features: [
      { icon: '⚔️', title: 'Статус війни', desc: 'Хто зіграв 4/4, хто не дограв, скільки часу до кінця воєнного дня.', pro: false },
      { icon: '🏁', title: 'Гонка кланів', desc: 'Місця та очки всіх кланів тижня, прогноз фіналу. Тап по клану — його минулі війни.', pro: false },
      { icon: '🏆', title: 'Рейтинг тижня та топ бота', desc: 'Хто більше всіх набив за поточний тиждень + глобальний топ гравців усіх кланів.', pro: false },
      { icon: '🔮', title: 'Прогноз клану', desc: 'Скільки медалей наберете за сьогодні та до кінця тижня, з діапазоном і точністю.', pro: false },
      { icon: '📅', title: 'Минулі війни та сезон', desc: 'Рейтинг гравців за кожну війну (W1, W2…) та сумарний залік за весь сезон.', pro: false },
      { icon: '📜', title: 'Історія по тижнях', desc: 'Графіки та динаміка клану і особисто твоя за минулі тижні воєн.', pro: false },
      { icon: '👉', title: '«Штовхнути» тих, хто не зіграв', desc: 'Надіслати нагадування всім одразу однією кнопкою (Free — до 20 осіб).', pro: false },
      { icon: '⏰', title: 'Автонагадування', desc: 'Особисте повідомлення кожному, хто не дограв, за N годин до кінця дня.', pro: true },
      { icon: '📊', title: 'Pro-аналітика', desc: "Шанс перемоги в гонці, здоров'я клану та архетип кожного гравця (Тащер, Надійний…).", pro: true },
    ],
  },

  playerModal: {
    close: 'Закрити',
    rankInClan: 'у клані',
    noTg: 'нема TG',
    reliability: 'надійність',
    weekMedals: '🏅 медалі за тиждень',
    avgAttack: '⚡ в середньому за атаку',
    totalAttacks: '⚔️ атак загалом',
    boatAttacks: '🚤 атаки човника',
    dayForecast: '🔮 прогноз на день',
    weekForecast: '🔮 прогноз тижня',
    repairPoints: '🔧 Очки ремонту:',
    forecastNote: '🔮 Прогноз = поточні медалі + очікувані атаки, що залишились, помножені на середні медалі за атаку цього гравця (за його минулими війнами). Чим більше історії — тим точніше.',
    pastWars: '📜 Минулі війни',
    loadingHistory: 'Завантажую історію…',
    historyError: 'Історія недоступна.',
    noHistory: 'Поки немає даних — історія накопичується з моменту підключення клану до бота.',
    royaleApi: 'Повний профіль на RoyaleAPI ↗',
    decksToday: 'колоди сьогодні:',
    played: 'Зіграв усі колоди',
    timeLeft: 'Ще є час',
    notPlayed: 'Не дограв — дедлайн близько',
    season: 'сезон',
  },

  nudge: {
    sending: 'Штовхаю…',
    label: 'Штовхнути ледарів',
    freeLimit: ' · ліміт 20 на Free',
    done: 'Штовхнув!',
    allPlayed: 'Всі вже зіграли 🎉',
    noWar: 'Зараз не день війни',
    error: 'Не вийшло — спробуй пізніше',
    dm: '✉️ в ЛП:',
    chat: '💬 в чат:',
    skipped: '⏸ нещодавно штовхали:',
  },

  reminder: {
    title: '⏰ Автонагадування',
    saved: '✓ збережено',
    desc1: 'Бот надішле особисте повідомлення в Telegram тим, хто не відіграв усі 4/4 колоди, за ',
    desc2: ' год до кінця воєнного дня (день закінчується о 10:00 UTC). У груповий чат іде загальне зведення.',
    dmTitle: '💬 Особисті нагадування:',
    dmFree: 'Free',
    dmPro: 'Pro',
    dmFreeDesc: '— до',
    dmFreeDescSuffix: 'гравців,',
    dmProDesc: "— всім прив'язаним без ліміту.",
    upsell1: '🔒 Зараз особисті нагадування йдуть',
    upsell2: 'з',
    upsell3: "прив'язаних гравців.",
    h: 'год',
  },

  link: {
    title: "Акаунт не прив'язаний",
    desc: 'Надішли боту в груповому чаті клану команду:',
    hint: 'Свій тег знайдеш у Clash Royale: профіль → значок під іменем.',
  },

  recruit: {
    tabLabel: 'Біржа',
    myTitle: '🔍 Шукаю клан',
    myHint: 'Твоя анкета буде видна лідерам кланів з тарифом Pro.',
    noteLabel: 'Про себе (необов'язково)',
    notePlaceholder: 'Активність, досвід, побажання…',
    activate: 'Розмістити анкету',
    deactivate: 'Зняти анкету',
    activeStatus: '✅ Анкета активна — лідери кланів тебе бачать',
    inactiveStatus: 'Анкета не активна',
    saving: '…',
    boardTitle: '👥 Біржа гравців',
    boardHint: 'Гравці, що шукають клан. Тап на картку — відкрити в Telegram.',
    empty: 'Поки ніхто не шукає клан 🤷',
    loading: 'Завантажую кандидатів…',
    error: 'Не вдалося завантажити список',
    weeks: 'тиж',
    medalsTotal: 'медалей',
    perAttack: 'за атаку',
    openTg: 'Написати ↗',
    ago: 'подав заявку',
  },

  owner: {
    title: '⚙️ Панель власника',
    clans: 'Кланів:',
    pro: 'Pro:',
    linked: "TG-прив'язок:",
    expiry: 'до',
    pro30: 'Pro 30д',
    pro90: 'Pro 90д',
    proInf: 'Pro ∞',
    free: 'Free',
    delete: '🗑 Видалити',
    confirmDelete: '❗ Точно видалити?',
    deleteHint: "Видалить клан, всі TG-прив'язки гравців та історію воєн. Натисни ще раз для підтвердження.",
    noClans: "Поки жодного клану — клани з'являються після /setup у групі.",
    error: 'Немає доступу або помилка мережі.',
  },
}

const en: Translations = {
  loading: 'Loading',
  loadingWar: 'Loading war status…',
  openViaTitle: 'Open the app via Telegram',
  openViaHint: "Go to your clan group or the bot chat and open the Mini App using the button — when opened via a regular link, Telegram doesn't pass login data.",
  serverError: 'Server error — check the API logs or try again later',
  networkError: 'Network error',
  retry: 'Retry',
  dateLocale: 'en-US',

  tabs: { war: 'War', rating: 'Rating', me: 'Me', search: 'Search', owner: 'Panel', recruit: 'Market' },

  period: { training: 'Training Days', warDay: 'War Day', colosseum: 'Coliseum' },
  header: { dayOf4: '· day', untilEnd: 'Until end of day:', h: 'h', played: 'played' },

  stats: { weekMedals: 'week medals', decksToday: 'decks today', medalsPerBattle: 'medals/battle', active: 'active' },

  race: {
    title: '⛵ Weekly Race',
    trainingNote: 'medals from Thursday',
    ours: 'us',
    todayTitle: 'Medals today',
    boatTitle: 'Boat points today',
    totalTitle: 'Total medals this week',
    projTitle: 'Projected medals by end of day (avg × 200)',
  },

  forecast: {
    titleSimple: '🔮 Forecast',
    title: '🔮 Clan Forecast',
    trainingNote: 'Training days are running — forecast will appear when the war starts.',
    lockedNote: 'How many medals the clan will earn today and this week, trend and forecast accuracy — available on the Pro plan. 🔒',
    trend: { ahead: 'Ahead of pace', onPace: 'On pace', behind: 'Behind pace' },
    dayLabel: '🏅 points for today',
    range: 'range:',
    weekLabel: '🏆 by end of week:',
    attacksLeft: 'attacks remaining: ~',
    accuracy: 'Forecast accuracy',
    howTitle: 'How is the forecast calculated?',
    howDay1: 'For today: medals earned today + ',
    howDay2: " remaining clan attacks × average medals per attack. We count only the current war day's points, not the cumulative week total.",
    howWeek: '«By end of week: same, but until the end of all 4 war days. "Range" is the ±1σ spread (how the actual result might land). The more days played and the more active the clan — the higher the accuracy.',
  },

  players: { empty: 'No war participants — training may be in progress.', noTg: 'no TG', decksDots: 'of 4 decks' },

  roles: { leader: 'Leader', coleader: 'Co-leader', elder: 'Elder' },

  insights: {
    title: '🧠 Clan Analytics',
    lockedNote: "Real-time win chance in the race, clan health, and DNA profiles of players (who carries, who's dead weight) — available on the Pro plan. 🔒",
    winCaption: 'win chance in race',
    topRival: 'top rival:',
    slackersWarn1: "If those who haven't finished don't play — the chance will drop to ",
    slackersWarn2: '%',
  },

  warlog: {
    title: '📜 War Log',
    hint: 'tap a week to see all clans',
    victory: '🥇 victory',
    placePrefix: 'Place ',
    placeSuffix: '',
    season: 'season',
    ours: 'us',
    close: 'Close',
    loading: 'Loading war log…',
    error: 'War log unavailable.',
    empty: 'This clan has no completed wars in the log yet.',
    pastWars: '📜 Past Wars',
  },

  history: {
    title: '📈 War History',
    lockedNote: 'Clan dynamics by week and war day — available on the Pro plan. 🔒',
    emptyNote: 'History accumulates automatically with each war day — check back after the first war day.',
    currentWeek: 'Current week',
    colosseum: '· Coliseum',
    day: 'Day',
    leaders: '👑 Leaders:',
    myContrib: 'Your contribution this week:',
  },

  search: {
    title: '🔍 Player Search',
    hint: "Enter a player tag (e.g. #ABC123) — we'll show the full profile.",
    placeholder: '#ABC123',
    find: 'Find',
    finding: '…',
    notFound: 'Player not found',
    royaleApi: 'RoyaleAPI ↗',
    level: 'Level',
    trophies: 'Trophies',
    warTrophies: 'CW Trophies',
    arena: 'Arena',
    warStats: '⚔️ War stats (from clan log)',
    weeks: 'Weeks',
    totalMedals: 'Total Medals',
    perAttack: 'Per Attack',
    cards: 'Cards',
    tabProfile: 'Profile',
    tabWars: 'CW Wars',
    lvl: 'lvl',
  },

  me: {
    notInWar: 'You are not in the current war',
    loadError: 'Failed to load stats',
    contrib: 'clan contribution',
    contribAria: 'Contribution',
    rankOf: 'rank',
    of: 'of',
    weekMedals: '🏅 weekly medals',
    decksToday: '🃏 decks today',
    medalsPerAttack: 'medals / attack',
    vsClan: '% vs clan average',
    weekAttacks: 'attacks this week',
    dayForecast: '🔮 day forecast',
    weekForecast: '🔮 week forecast',
    seasonTitle: '🗓 Season #',
    seasonWeeks: 'weeks:',
    seasonMedals: '🏅 season medals',
    seasonRankSuffix: 'in ranking',
    bestWeek: '⚡ best week',
    weeksIn: 'weeks participated',
    share: '📤 Share results',
    shareHeader: '⚔️ My Clan War results',
    shareFame: '🏅 Fame:',
    shareAvg: '⚡',
    shareWeekForecast: '🔮 Week forecast:',
    shareSeason: '🗓 Season:',
    shareContrib: 'My contribution:',
    shareContribSuffix: '% of clan medals 💪',
    medalsPerAttackUnit: 'medals per attack',
    perf: { top: 'top', above: 'above average', mid: 'average', below: 'below average' },
  },

  leaderboard: {
    title: '🏆 Leaderboard',
    period: 'Leaderboard period',
    current: '📅 Current War',
    season: '🏆 Full Season',
    global: '🌍 Bot Top',
    noDataCurrent: 'No leaderboard data yet.',
    mvpWeek: '👑 Week MVP:',
    mvpSeason: '👑 Season MVP:',
    mvpBot: '👑 Bot Champion:',
    medals: 'medals',
    you: 'you',
    weeks: 'wk',
    perAttack: '/attack',
    globalEmpty: 'Players from all bot clans compete here after linking their account via /link. Data accumulates with each war week.',
    globalMeta: 'Players from all bot clans · window:',
    globalMetaWeeks: 'weeks · participants:',
    seasonEmpty: 'Season data will appear after the first war day.',
    seasonMeta: 'Season #',
    seasonMetaWeeks: '· weeks in ranking:',
    seasonMetaSuffix: '(W1 → current)',
    best: 'best',
    streakTitle: 'weeks in a row',
  },

  about: {
    title: 'ℹ️ How it works',
    intro: 'The bot tracks the Clan War: 4 war days, each player has 4 decks per day. All numbers come from the official Clash Royale API, forecasts are based on past war history.',
    upsell: '🔒 Pro-tagged features unlock on the Pro plan. Contact the bot owner to upgrade.',
    features: [
      { icon: '⚔️', title: 'War Status', desc: "Who played 4/4, who hasn't finished, how much time until the end of the war day.", pro: false },
      { icon: '🏁', title: 'Clan Race', desc: 'Standings and points of all clans this week, final forecast. Tap a clan to see its past wars.', pro: false },
      { icon: '🏆', title: 'Weekly rating & bot top', desc: 'Who scored the most this week + global top players from all clans.', pro: false },
      { icon: '🔮', title: 'Clan Forecast', desc: "How many medals you'll earn today and by end of week, with range and accuracy.", pro: false },
      { icon: '📅', title: 'Past Wars & Season', desc: 'Player rankings for each war (W1, W2…) and cumulative season standings.', pro: false },
      { icon: '📜', title: 'Weekly History', desc: 'Charts and dynamics for the clan and your personal stats across past war weeks.', pro: false },
      { icon: '👉', title: 'Nudge slackers', desc: 'Send a reminder to everyone at once with one button (Free — up to 20 people).', pro: false },
      { icon: '⏰', title: 'Auto-reminders', desc: "Personal message to each player who hasn't finished N hours before the day ends.", pro: true },
      { icon: '📊', title: 'Pro Analytics', desc: 'Win chance in the race, clan health, and player archetypes (Carry, Reliable…).', pro: true },
    ],
  },

  playerModal: {
    close: 'Close',
    rankInClan: 'in clan',
    noTg: 'no TG',
    reliability: 'reliability',
    weekMedals: '🏅 weekly medals',
    avgAttack: '⚡ avg per attack',
    totalAttacks: '⚔️ total attacks',
    boatAttacks: '🚤 boat attacks',
    dayForecast: '🔮 day forecast',
    weekForecast: '🔮 week forecast',
    repairPoints: '🔧 Repair points:',
    forecastNote: "🔮 Forecast = current medals + expected remaining attacks × this player's average medals per attack (from past wars). The more history — the more accurate.",
    pastWars: '📜 Past Wars',
    loadingHistory: 'Loading history…',
    historyError: 'History unavailable.',
    noHistory: 'No data yet — history accumulates from the moment the clan connects to the bot.',
    royaleApi: 'Full profile on RoyaleAPI ↗',
    decksToday: 'decks today:',
    played: 'Played all decks',
    timeLeft: 'Still time left',
    notPlayed: "Didn't finish — deadline is near",
    season: 'season',
  },

  nudge: {
    sending: 'Nudging…',
    label: 'Nudge slackers',
    freeLimit: ' · limit 20 on Free',
    done: 'Nudged!',
    allPlayed: 'Everyone has played 🎉',
    noWar: 'Not a war day',
    error: 'Failed — try again later',
    dm: '✉️ DMs:',
    chat: '💬 to chat:',
    skipped: '⏸ recently nudged:',
  },

  reminder: {
    title: '⏰ Auto-reminders',
    saved: '✓ saved',
    desc1: "The bot will send a personal Telegram message to those who haven't played all 4/4 decks, ",
    desc2: 'h before the war day ends (day ends at 10:00 UTC). A summary is posted to the group chat.',
    dmTitle: '💬 Personal reminders:',
    dmFree: 'Free',
    dmPro: 'Pro',
    dmFreeDesc: '— up to',
    dmFreeDescSuffix: 'players,',
    dmProDesc: '— for all linked players without limit.',
    upsell1: '🔒 Currently personal reminders go to',
    upsell2: 'of',
    upsell3: 'linked players.',
    h: 'h',
  },

  link: {
    title: 'Account not linked',
    desc: "Send this command to the bot in your clan's group chat:",
    hint: 'Find your tag in Clash Royale: profile → icon below your name.',
  },

  recruit: {
    tabLabel: 'Market',
    myTitle: '🔍 Looking for a clan',
    myHint: 'Your profile will be visible to clan leaders on the Pro plan.',
    noteLabel: 'About me (optional)',
    notePlaceholder: 'Activity, experience, preferences…',
    activate: 'Post profile',
    deactivate: 'Remove profile',
    activeStatus: '✅ Profile active — clan leaders can see you',
    inactiveStatus: 'Profile not active',
    saving: '…',
    boardTitle: '👥 Player Market',
    boardHint: 'Players looking for a clan. Tap a card to open in Telegram.',
    empty: 'Nobody is looking for a clan yet 🤷',
    loading: 'Loading candidates…',
    error: 'Failed to load list',
    weeks: 'wk',
    medalsTotal: 'medals',
    perAttack: 'per attack',
    openTg: 'Message ↗',
    ago: 'applied',
  },

  owner: {
    title: '⚙️ Owner Panel',
    clans: 'Clans:',
    pro: 'Pro:',
    linked: 'TG links:',
    expiry: 'until',
    pro30: 'Pro 30d',
    pro90: 'Pro 90d',
    proInf: 'Pro ∞',
    free: 'Free',
    delete: '🗑 Delete',
    confirmDelete: '❗ Confirm delete?',
    deleteHint: 'Will delete the clan, all player TG links and war history. Click again to confirm.',
    noClans: 'No clans yet — clans appear after /setup in the group.',
    error: 'No access or network error.',
  },
}

const TRANSLATIONS: Record<Lang, Translations> = { ru, uk, en }

function detectLang(): Lang {
  try {
    const saved = localStorage.getItem('cwst_lang') as Lang | null
    if (saved && saved in TRANSLATIONS) return saved
  } catch { /* ignore */ }
  const tgLang = (window as unknown as { Telegram?: { WebApp?: { initDataUnsafe?: { user?: { language_code?: string } } } } })
    .Telegram?.WebApp?.initDataUnsafe?.user?.language_code
  if (tgLang === 'uk') return 'uk'
  if (tgLang && tgLang.startsWith('en')) return 'en'
  return 'ru'
}

interface LangCtxValue {
  lang: Lang
  setLang: (l: Lang) => void
  t: Translations
}

const LangCtx = createContext<LangCtxValue>({ lang: 'ru', setLang: () => {}, t: ru })

export function LangProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(detectLang)

  const setLang = (l: Lang) => {
    try { localStorage.setItem('cwst_lang', l) } catch { /* ignore */ }
    setLangState(l)
  }

  return <LangCtx.Provider value={{ lang, setLang, t: TRANSLATIONS[lang] }}>{children}</LangCtx.Provider>
}

export function useT() {
  return useContext(LangCtx)
}

/** Translate a role string from the server (always Russian) */
export function roleLabel(serverRole: string | undefined, t: Translations): string | undefined {
  if (!serverRole) return undefined
  if (serverRole === 'Лидер') return t.roles.leader
  if (serverRole === 'Соруководитель') return t.roles.coleader
  if (serverRole === 'Старейшина') return t.roles.elder
  return serverRole
}

/** Translate performance label from server (always Russian) */
export function perfLabel(serverLabel: string, t: Translations): string {
  if (serverLabel === 'топ') return t.me.perf.top
  if (serverLabel === 'выше среднего') return t.me.perf.above
  if (serverLabel === 'средне') return t.me.perf.mid
  if (serverLabel === 'ниже среднего') return t.me.perf.below
  return serverLabel
}

/** Format race position (1 = victory, rest = place N) */
export function formatPlace(rank: number, t: Translations): string {
  if (rank === 1) return t.warlog.victory
  return `${t.warlog.placePrefix}${rank}${t.warlog.placeSuffix}`
}
