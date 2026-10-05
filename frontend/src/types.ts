export type PlayStatus = 'played' | 'timeLeft' | 'notPlayed'

export interface PlayerStatus {
  playerTag: string
  name: string
  decksUsedToday: number
  decksUsed: number          // суммарно за неделю (включая тренировку)
  warDecksUsed: number       // только военные атаки
  fame: number
  repairPoints: number
  boatAttacks: number
  avgFamePerAttack: number
  projectedDayFame: number
  projectedWeekFame: number
  rank: number               // место в клане по славе
  status: PlayStatus
  isLinked: boolean
  consecutiveWars: number    // недель подряд участвовал (0 — не участвовал)
  role?: string              // "leader" | "coLeader" | "elder" | undefined (рядовой)
  trophies: number           // кубки игрока (0 — состав клана не отдался)
  dnaLabel?: string          // архетип ("Тащер 💪" и т.п.), undefined — мало данных
  reliabilityScore: number   // надёжность 0..100 (0 — нет данных)
  isSponsor?: boolean        // спонсор — ★ рядом с именем
  backgroundKey?: BackgroundKey | null  // оформление строки спонсора
  badgeKey?: string | null   // выставленный напоказ значок
  badgeLevel?: number        // 1 бронза, 2 серебро, 3 золото
}

/* --- Дисциплина клана: кто подводит и кого приходится тянуть --- */
export interface DisciplinePlayer {
  playerTag: string
  name: string
  missedDecks: number        // недоигранные военные колоды за учтённые недели
  missedWeeks: number        // недель в составе без единой атаки
  weeksTracked: number       // сколько недель игрок был в составе
  nudgeCount: number         // сколько раз бот его пинал
  lastMinuteBattles: number  // бои в последний час перед концом дня
  totalBattles: number
  avgHoursBeforeEnd: number  // в среднем за сколько часов до конца отыгрывает
}

/* --- Разведка гонки: досье на кланы недели --- */
export interface ScoutClan {
  tag: string
  name: string
  position: number
  isOurClan: boolean
  currentFame: number
  dayPoints: number[]        // очки по завершённым дням этой недели
  weeksTracked: number       // 0 — истории нет
  avgWeekFame: number
  bestWeekFame: number
  avgRank: number
  volatility: number         // 0..100, больше — непредсказуемее
  avgDecksPerPlayer: number  // из 16
  avgParticipants: number
  paceVsUsualPercent: number // +12 = на 12% выше своего обычного
  fadesLate: boolean
}

export interface RaceScout {
  weeksAnalyzed: number
  clans: ScoutClan[]
  /** С кем реально идёт борьба (по обычной силе, а не по сегодняшнему месту). */
  realRivalTag: string | null
}

export interface ClanDiscipline {
  weeksAnalyzed: number
  skippers: DisciplinePlayer[]
  nudged: DisciplinePlayer[]
  lastMinute: DisciplinePlayer[]
}

/** Аналитика: шанс победы и здоровье клана. */
export interface HealthFactor {
  name: string
  score: number              // 0..100
}

export interface ClanInsights {
  winChance: number | null            // % победы; null — тренировка
  winChanceIfSlackersOut: number | null
  topRivalName: string | null
  healthScore: number                 // 0..100
  healthLabel: string
  factors: HealthFactor[]
}

export interface ClanStats {
  totalFame: number
  totalRepairPoints: number
  totalDecksUsedToday: number
  totalDecksUsedWeek: number
  maxDecksToday: number
  activePlayers: number
  playersPlayed: number
  playersNotPlayed: number
  avgFamePerAttack: number
}

export type ForecastTrend = 'ahead' | 'onPace' | 'behind'

export interface ClanForecast {
  projectedDayFame: number
  projectedWeekFame: number
  expectedRemainingAttacksToday: number
  confidence: number         // 0..100
  trend: ForecastTrend
  projectedDayFameLow: number   // -1σ нижняя граница прогноза дня
  projectedDayFameHigh: number  // +1σ верхняя граница прогноза дня
}

/** Один клан в таблице гонки недели. */
export interface RaceClan {
  tag: string
  name: string
  position: number           // 1..5
  fame: number               // медали за ВСЮ неделю (накопленные)
  todayFame: number          // медали за бои только сегодня (periodPoints из CR API)
  boatPoints: number         // очки лодки сегодня (clan.fame из CR API)
  projectedFame: number      // прогноз медалей к концу дня
  avgFamePerAttack: number   // война: сегодня; колизей: за всю неделю
  decksUsedToday: number
  maxDecksToday: number
  decksUsed: number          // колоды за всю неделю (для колизея)
  isColosseum: boolean       // колизей — своя логика (без «сегодня» и лодок)
  warTrophies: number        // КВ-трофеи клана (0 — не удалось получить)
  isOurClan: boolean
  isFinished: boolean
}

/** История войн игрока (по данным сервиса). */
export interface PlayerWeekHistory {
  seasonId: number
  sectionIndex: number
  isColosseum: boolean
  clanTag: string
  clanName: string
  fame: number
  decksUsed: number
  avgFamePerAttack: number
  clanAvgFamePerAttack: number
}

export interface PlayerHistory {
  playerTag: string
  royaleApiUrl: string
  weeks: PlayerWeekHistory[]
}

export interface WarLogPlayer {
  playerTag: string
  name: string
  fame: number
  decksUsed: number
}

/** Журнал прошлых войн (официальный riverracelog): места кланов и очки. */
export interface WarLogClan {
  rank: number               // 1..5
  tag: string                // по нему открывается страница клана
  name: string
  fame: number               // медали клана за неделю
  trophyChange: number       // +/- КВ-трофеи по итогам
  isOurClan: boolean
  players?: WarLogPlayer[]
}

export interface WarLogWeek {
  seasonId: number
  sectionIndex: number       // неделя внутри сезона (0..3)
  isColosseum: boolean
  standings: WarLogClan[]    // отсортированы по месту
}

/** Журнал войн произвольного клана (модалка из гонки). */
export interface ClanWarLog {
  clanTag: string
  weeks: WarLogWeek[]        // isOurClan в standings помечает запрошенный клан
}

/* --- Рейтинг клана по КВ-трофеям (страна/мир, официальные rankings) --- */
export interface RankedClanRow {
  rank: number
  previousRank: number
  tag: string                // по нему открывается страница клана
  name: string
  warTrophies: number
  members: number
  isOurClan: boolean
}

export interface ClanRanking {
  warTrophies: number
  countryName: string | null
  countryRank: number | null
  countryPreviousRank: number | null
  globalRank: number | null
  globalPreviousRank: number | null
  countryTop: RankedClanRow[]
}

/* --- Журнал военных боёв (кто/когда отыграл КВ + исход) --- */
export interface WarBattleEntry {
  playerName: string
  playerTag: string
  battleTimeUtc: string
  won: boolean
  crownsFor: number
  crownsAgainst: number
}

export interface WarJournal {
  won: number
  lost: number
  total: number
  battles: WarBattleEntry[]   // новые первыми
}

export interface WarDayLog {
  dayIndex: number              // 0..6 (нормализованный день недели гонки)
  pointsEarned: number          // очки клана за день
  endOfDayRank: number          // место клана на конец дня (1..5)
  numOfDefensesRemaining: number
  weekOffset: number            // 0 = текущая неделя, 1 = прошлая…
}

export interface ClanStatus {
  clanTag: string
  clanName: string
  periodType: 'training' | 'warDay' | 'colosseum'
  periodIndex: number
  dayEndsAtUtc: string
  hoursLeft: number
  stats: ClanStats
  forecast: ClanForecast | null
  race: RaceClan[]                // ситуация в гонке (все кланы недели)
  players: PlayerStatus[]
  insights: ClanInsights | null   // аналитика недели
  warLog: WarLogWeek[]            // журнал прошлых войн (места кланов и очки)
  dayLogs: WarDayLog[]            // официальный по-дневный лог гонки (periodLogs из API)
  myPlayerTag?: string       // тег текущего пользователя (если /my/status)
  isAdmin?: boolean          // админ ли текущий пользователь в группе клана
  isClanLeader?: boolean     // leader или coLeader в CR-клане
  isOwner?: boolean          // владелец сервиса (видит панель ⚙️)
  clanBackgroundKey?: BackgroundKey | null  // тема клана, выбранная спонсором
  viewingAsAdmin?: boolean   // это чужой клан, открытый из панели
  adminReadOnly?: boolean    // зашёл модератор: смотреть можно, менять нельзя
  reminderHoursBeforeEnd?: number // за сколько часов до конца дня шлём автонапоминания
}

/** Уровень доступа к сервису целиком. */
export type ServiceRole = 'none' | 'moderator' | 'owner'

/**
 * Права, выдаваемые поимённо. Имена совпадают с флагами на сервере: список ходит
 * строками, а не числом, чтобы разбор битов не пришлось повторять второй копией
 * на фронте — две копии правил расходятся всегда.
 */
export type ServicePermission =
  | 'EnterClans'
  | 'ManageClans'
  | 'ChatAdmin'
  | 'Plans'
  | 'Broadcast'
  | 'DeleteClans'
  | 'ManageModerators'
  | 'Maintenance'
  | 'Sponsors'
  | 'AppSettings'

export interface ServiceIdentity {
  role: ServiceRole
  permissions: ServicePermission[]
}

export interface Moderator {
  id: number
  username: string
  note: string | null
  permissions: ServicePermission[]
  addedAtUtc: string
  firstSeenAtUtc: string | null
  /** false — человек ещё ни разу не заходил, запись держится на юзернейме. */
  confirmed: boolean
}

export interface MySeason {
  seasonId: number
  totalFame: number
  rank: number
  clanSize: number
  weeksParticipated: number
  bestWeekFame: number
  weeksTracked: number
}

/* --- «Что нового»: персональная дельта с прошлого визита --- */
export interface WhatsNew {
  isFirstVisit: boolean
  lastVisitAtUtc: string | null
  fameDelta: number
  rankDelta: number          // +N = поднялся на N мест
  rank: number
  respectsSince: number
  passedByName: string | null
  decksLeftToday: number
  badgesEarned: string[]
}

/* --- Респекты 👏 --- */
export interface RespectStatus {
  givenToday: boolean
  givenToName: string | null
  myTotal: number
}

/* --- Витрина наград: значки с уровнями и прогрессом (эффект владения + Зейгарник) --- */
export interface Achievement {
  key: 'streak' | 'dailyStreak' | 'perfectDays' | 'mvpWeeks' | 'totalFame' | 'warsPlayed'
       | 'perfectWeeks' | 'perfectSeasons' | 'boatAttacks'
  level: number          // 0 нет, 1 бронза, 2 серебро, 3 золото
  value: number
  nextAt: number | null  // порог следующего уровня, null = золото
  thresholds: number[]
}

export interface Achievements {
  playerTag: string
  badges: Achievement[]
  weeksAnalyzed: number
  /** Ключи наград, открытых с прошлого просмотра. Приходят ровно один раз. */
  justUnlocked: string[]
  /** Какой значок выставлен напоказ. null — не выбран. */
  showcaseKey?: string | null
}

export interface MyStats {
  playerTag: string
  name: string
  clanName: string
  fame: number
  repairPoints: number
  boatAttacks: number
  decksUsedToday: number
  decksUsed: number
  avgFamePerAttack: number
  projectedDayFame: number
  projectedWeekFame: number
  rank: number
  clanSize: number
  contributionPercent: number
  performanceLabel: string
  clanAvgFamePerAttack: number
  season: MySeason | null    // null — данных ещё нет
}

export interface NudgeResult {
  notifiedDm: number
  skippedCooldown: number
  taggableCount: number
  unlinkedCount: number
  postedToChat: boolean
}

/* --- История войн --- */
export interface DayHistory {
  periodIndex: number
  dayNumber: number          // 1..4
  capturedAtUtc: string
  totalFame: number
  dayFame: number
}

export interface TopPlayer {
  playerTag: string
  name: string
  fame: number
}

export interface WeekHistory {
  seasonId: number
  sectionIndex: number
  isColosseum: boolean
  finalFame: number
  myFame: number | null
  days: DayHistory[]
  topPlayers: TopPlayer[]
}

export interface ClanHistory {
  weeks: WeekHistory[]
}

/* --- Сезонный зачёт --- */
export interface SeasonPlayer {
  playerTag: string
  name: string
  totalFame: number
  weeksParticipated: number
  bestWeekFame: number
  rank: number
}

export interface SeasonStats {
  seasonId: number
  weeksTracked: number
  players: SeasonPlayer[]
}

/* --- Архив прошлых сезонов: топ игроков за каждый завершённый сезон --- */
export interface SeasonArchiveEntry {
  seasonId: number
  weeksTracked: number
  clanTotalFame: number
  topPlayers: SeasonPlayer[]
}

export interface SeasonArchive {
  seasons: SeasonArchiveEntry[]
}

/* --- Разбивка сезона по неделям: каждая война + общий зачёт --- */
export interface SeasonWeekPlayer {
  playerTag: string
  name: string
  fame: number
  decksUsed: number
  rank: number
}

export interface SeasonWeek {
  sectionIndex: number
  label: string              // "Война 1" / "Колизей"
  isColosseum: boolean
  isCurrent: boolean         // эта неделя идёт прямо сейчас
  clanFame: number
  players: SeasonWeekPlayer[]
}

export interface SeasonBreakdown {
  seasonId: number
  currentSectionIndex: number
  weeks: SeasonWeek[]        // по возрастанию (Война 1, Война 2, …)
  seasonTotal: SeasonPlayer[]
}

/* --- Профиль игрока (поиск по тегу) --- */
export interface PlayerCard {
  name: string
  level: number
  maxLevel: number
  iconUrl: string
  evolutionLevel: number      // >0 — эволюция открыта игроком
  maxEvolutionLevel: number   // >0 — у карты вообще есть эволюция
  evoIconUrl: string | null
}

export interface PathOfLegend {
  trophies: number
  leagueNumber: number
  rank: number
}

export interface PlayerProfile {
  playerTag: string
  name: string
  expLevel: number
  trophies: number
  bestTrophies: number
  clanWarTrophies: number
  clanName: string | null
  clanTag: string | null
  arenaName: string | null
  cards: PlayerCard[]
  weeksPlayed: number
  totalFame: number
  avgFamePerAttack: number
  warDayWins: number
  battleCount: number
  threeCrownWins: number
  currentWinLoseStreak: number
  currentPathOfLegend: PathOfLegend | null
  bestPathOfLegend: PathOfLegend | null
  currentFavouriteCard: string | null
  currentDeck: PlayerCard[]
  wins: number
  losses: number
  maxCardLevel: number          // текущий потолок уровня карт в игре
  analysis: PlayerAnalysis | null  // разбор под набор; null — нет колоды
  royaleApiUrl: string
}

export interface DuelSheetRank {
  trophies: number
  peak: number
  league: DuelLeagueKey
  division: number
  wins: number
  losses: number
  place: number
}

export type ClanLeague = 'bronze' | 'silver' | 'gold' | 'legendary'

/**
 * Разбор профиля: в клан какой лиги идти. С сервера приходят только числа и код лиги —
 * весь текст собирается здесь, чтобы он говорил на языке интерфейса.
 */
export interface PlayerAnalysis {
  league: ClanLeague
  nextLeague: ClanLeague | null      // null — уже легендарная
  warLevel: number                   // средний уровень 32 лучших карт
  nextLeagueWarLevel: number | null  // до какого боевого уровня тянуть
  nextLeagueMaxedCards: number | null // сколько карт добить до потолка
  avgDeckLevel: number
  maxCardLevel: number
  maxedInDeck: number
  deckSize: number
  maxedTotal: number
  cardsTotal: number
  fullDecks: number             // сколько полных колод максимального уровня собирается
  decksNeeded: number           // нужно на полный военный день (4 боя = 4 колоды)
  warCardsNeeded: number        // те же 4 колоды в картах (32)
  evoUnlocked: number
  evoAvailable: number
  winRate: number | null        // null — боёв мало, процент был бы случайностью
  warDayWins: number
  weeksPlayed: number
  avgFamePerAttack: number
}

/* --- Глобальный топ бота (все кланы, привязанные игроки) --- */
export interface GlobalTopPlayer {
  playerTag: string
  name: string
  clanName: string
  totalFame: number
  weeksParticipated: number
  bestWeekFame: number
  avgFamePerAttack: number
  rank: number
  isMe: boolean
}

export interface GlobalTop {
  weeksWindow: number
  playersTracked: number
  players: GlobalTopPlayer[]
}

/* --- Мировой топ: мета, список, карточка игрока --- */
export interface TopDeckCard {
  cardId: number
  name: string
  iconUrl: string
}

export interface TopCard {
  cardId: number
  name: string
  iconUrl: string
  /** Доля колод топа, где карта встречается. */
  percent: number
  /** Изменение доли за неделю в п.п.; null — сравнивать не с чем. */
  deltaPercent: number | null
}

export interface TopMeta {
  dayUtc: string
  comparedToDayUtc: string | null
  playersTracked: number
  /** У скольких колода известна — проценты считаются от этого числа. */
  playersWithDeck: number
  cutoffTrophies: number
  topTrophies: number
  avgLevel: number
  cards: TopCard[]
}

/* --- Мета дня: колоды топа по боям --- */
export interface MetaCard {
  cardId: number
  name: string
  iconUrl: string
  /** Эволюция — иконка тогда эволюционная. */
  evo: boolean
  elixir: number
}

export interface MetaCounter {
  card: MetaCard
  /** Процент побед колоды, когда у соперника есть эта карта. */
  winPercent: number
  /** На сколько пунктов ниже обычного для колоды (отрицательное). */
  deltaPercent: number
  games: number
}

export interface MetaDeckRow {
  cards: MetaCard[]
  games: number
  wins: number
  draws: number
  losses: number
  winPercent: number
  usagePercent: number
  avgElixir: number
  /** Четыре самые дешёвые карты — полный цикл. */
  cycleElixir: number
  counters: MetaCounter[]
  copyLink: string | null
}

export interface TopProfileDeck {
  cards: MetaCard[]
  /** Сколько игроков топа держат эту колоду сейчас. */
  players: number
  bestRank: number
  copyLink: string | null
}

export interface MetaDecks {
  fromDayUtc: string
  toDayUtc: string
  battles: number
  decks: MetaDeckRow[]
  /** Только в выборке по карте: колоды из профилей топа. */
  profileDecks?: TopProfileDeck[] | null
}

/* --- Единая карточка игрока --- */
export interface SheetBattle {
  timeUtc: string
  type: string
  /** 1 — победа, 0 — ничья, -1 — поражение. */
  result: number
  crownsFor: number
  crownsAgainst: number
  opponentName: string | null
  opponentTag: string | null
  trophyChange: number | null
  myDeck: MetaCard[]
  opponentDeck: MetaCard[]
}

export interface PlayerSheet {
  playerTag: string
  name: string
  expLevel: number
  clanName: string | null
  clanTag: string | null
  role: string | null
  arenaName: string | null
  trophies: number
  bestTrophies: number
  /** Рейтинг Пути легенд в текущем сезоне; null — не играл. */
  rating: number | null
  ratingLeague: number | null
  ratingRank: number | null
  bestRating: number | null
  wins: number
  losses: number
  threeCrownWins: number
  battleCount: number
  warDayWins: number
  clanWarTrophies: number
  currentStreak: number
  favouriteCard: MetaCard | null
  /** Место в мировом топе по последнему снимку; null — не в топе. */
  worldRank: number | null
  inBot: boolean
  isSponsor: boolean
  backgroundKey: BackgroundKey | null
  badgeKey: string | null
  badgeLevel: number
  deck: MetaCard[]
  deckElixir: number
  deckLink: string | null
  battles: SheetBattle[]
  /** Боёв за 30 дней, сохранённых ботом (только у тех, кто в боте). */
  games30: number
  winPercent30: number
  royaleApiUrl: string
  /** Ранг в лиге дуэлей Clanify; null — не вступал. */
  duel?: DuelSheetRank | null
}

/* --- Личный разбор боёв --- */
export interface ElixirLeak {
  avgLeakWins: number
  avgLeakLosses: number
  avgLeakAll: number
  games: number
}

export interface MyDeck {
  cards: MetaCard[]
  games: number
  wins: number
  winPercent: number
  /** Процент побед такой же или похожей колоды у топа; null — в мете её нет. */
  metaWinPercent: number | null
  /** Сколько карт совпадает с колодой топа (8 — та же самая). */
  metaSharedCards: number
  metaGames: number
  metaCounters: MetaCounter[]
  copyLink: string | null
}

export interface TimeSlot {
  /** night/morning/day/evening или '0'..'6' (пн..вс). */
  key: string
  games: number
  winPercent: number
}

export interface Tilt {
  afterTwoLossesGames: number
  afterTwoLossesWinPercent: number
  afterWinGames: number
  afterWinWinPercent: number
  longestLossStreak: number
}

export interface ToughCard {
  card: MetaCard
  games: number
  winPercent: number
  deltaPercent: number
}

/** Доступ к полному разбору. */
export interface ReviewAccess {
  paywall: boolean
  /** Полный разбор открыт: Плюс есть или платное выключено. */
  unlocked: boolean
  active: boolean
  until: string | null
  source: string | null
}

/** Что спрятано за Плюсом — цифрами самого игрока. */
export interface ReviewLocked {
  toughCards: number
  decks: number
  counters: number
  weekdays: boolean
}

/** «Clanify Плюс» и спонсорство — одна линейка: статус, цены, подарки, оповещения. */
export interface PlusStatus {
  paywall: boolean
  active: boolean
  unlocked: boolean
  until: string | null
  /** purchase / gift / grant / sponsor */
  source: string | null
  price7: number
  price30: number
  onSale: boolean
  linked: boolean
  myTag: string | null
  tiltAlerts: boolean
  dmBlocked: boolean
  freeSignalsLeft: number
  /** 0 — спонсорство звёздами не продаётся. */
  sponsorPrice: number
  sponsorDays: number
  isSponsor: boolean
  sponsorUntil: string | null
  /** Бесплатные подарочные недели спонсора на этот месяц. */
  freeGiftsLeft: number
  /** Личная скидка на себя (после челленджа); null — нет. */
  promo7?: number | null
  promo30?: number | null
  promoUntil?: string | null
}

/** Экран «🧊 Стоп-тильт». */
export interface TiltProfile {
  games: number
  minBattles: number
  after2Games: number
  minSamples: number
  /** ice / boiling / volcano; null — рано судить. */
  type: string | null
  prevType: string | null
  basePercent: number
  after2Percent: number | null
  weekTiltBattles: number
  weekExtraLosses: number
  weekTiltTrophies: number
  prevWeekExtraLosses: number
  /** W / L / D от старых к новым. */
  lastSeries: string
  moments14: number
  moments14Wins: number
  moments14Losses: number
  recentStreak: { atUtc: string; length: number; trophies: number } | null
  paywall: boolean
  unlocked: boolean
  active: boolean
  enabled: boolean
  freeSignalsLeft: number
  dmBlocked: boolean
  lossThreshold: number
  dailyLossLimit: number | null
  quietHours: boolean
  pauseUntil: string | null
  alerts: number
  pausedPercent: number | null
  notPausedPercent: number | null
  recentAlerts: {
    sentUtc: string
    kind: string
    lossStreak: number
    choice: string | null
    afterWins: number
    afterLosses: number
    summarized: boolean
  }[]
}

export interface OwnerPlus {
  paywall: boolean
  price7: number
  price30: number
  active: number
  buyers: number
  purchases: number
  stars: number
  starsWeek: number
  gifts: number
  alertsWeek: number
  pausedWeek: number
  mutedWeek: number
  freeUsers: number
  freeThenBought: number
  tracker?: {
    enabled: number
    total: number
    offWeek: number
    mutesWeek: number
    cardsWeek: number
    dm: boolean
    beta: string
  }
  recent: {
    telegramUserId: number
    playerTag: string | null
    source: string
    days: number
    stars: number
    untilUtc: string
    createdAtUtc: string
    revoked: boolean
  }[]
}

export interface BattleAnalysis {
  playerTag: string
  games: number
  wins: number
  losses: number
  draws: number
  winPercent: number
  sinceUtc: string | null
  elixir: ElixirLeak | null
  decks: MyDeck[]
  dayParts: TimeSlot[]
  weekdays: TimeSlot[]
  tilt: Tilt | null
  toughCards: ToughCard[]
  metaBattles: number
  access: ReviewAccess
  /** null — ничего не спрятано. */
  locked: ReviewLocked | null
}

export interface TopPlayerRow {
  rank: number
  playerTag: string
  name: string
  clanName: string | null
  trophies: number
  expLevel: number
  deck: TopDeckCard[]
}

export interface TopBattle {
  battleTimeUtc: string
  type: string
  won: boolean
  crownsFor: number
  crownsAgainst: number
  opponentName: string | null
  myDeck: TopDeckCard[]
  opponentDeck: TopDeckCard[]
}

export interface TopPlayerDetail {
  playerTag: string
  name: string
  clanName: string | null
  trophies: number
  bestTrophies: number
  expLevel: number
  wins: number
  losses: number
  threeCrownWins: number
  rank: number | null
  currentDeck: TopDeckCard[]
  battles: TopBattle[]
  /** Рейтинг Пути легенд в текущем сезоне; null — не играл. */
  rating?: number | null
}

/* --- Панель владельца --- */
export interface OwnerStats {
  // Кланы
  totalClans: number
  chatsWithBot: number
  activeClans7d: number
  silentClans: number
  // Пользователи
  totalLinkedUsers: number
  usersWithClan: number
  usersWithoutClan: number
  usersWithUsername: number
  usersReachableByDm: number   // остальным бот не может написать первым
  invitedUsers: number
  // Рост (только по записям с известной датой)
  newClans7d: number
  newClans30d: number
  newUsers7d: number
  newUsers30d: number
  clansWithKnownDate: number
  usersWithKnownDate: number
  // Вовлечённость
  respects7d: number
  avgLinkedPerClan: number
  /** Приход по дням за 90 суток, от старых к свежим. Дни без привязок тоже здесь, с нулями. */
  signups: SignupPoint[]
}

export interface SignupPoint {
  date: string        // YYYY-MM-DD
  users: number       // привязалось новых игроков
  clans: number       // подключилось новых кланов
  active: number      // заходило в приложение
  acting: number      // из них что-то сделало, а не только посмотрело
}

export type NotifyChannel = 'dm' | 'chat' | 'both'

/** Язык, на котором бот пишет клану. Отдельно от языка интерфейса приложения. */
export type BotLang = 'ru' | 'uk' | 'en'

export interface NotificationSettings {
  language: BotLang
  reminderHoursBeforeEnd: number
  remindersEnabled: boolean
  remindersChannel: NotifyChannel
  warStartEnabled: boolean
  warStartChannel: NotifyChannel
  finalCallEnabled: boolean
  dailyReportEnabled: boolean
  warEndMinuteUtc: number | null   // во сколько заканчивается КВ (минуты от 00:00 UTC), null = 10:00 по умолчанию
  perfectDayEnabled: boolean       // поздравление «900 за день» в чат
  acceptsClanMail: boolean         // принимать ли сообщения от других кланов
}

export type BroadcastTarget = 'dm' | 'chats' | 'both'

export interface BroadcastResult {
  sentDm: number
  sentChats: number
  failedDm: number
  failedChats: number
}

export interface OwnerClan {
  id: number
  clanTag: string
  name: string
  linkedPlayers: number
  hasChat: boolean
  createdAtUtc: string | null
  lastActivityUtc: string | null
  isActive: boolean             // была активность за неделю
}

export interface OwnerMember {
  playerTag: string
  name: string
  telegramUsername: string | null
  telegramUserId: number | null
  role: string | null           // leader | coLeader | elder | member
  isLeader: boolean
  linkedAtUtc: string | null
}

export interface OwnerClanDetail {
  id: number
  clanTag: string
  name: string
  telegramChatId: number
  telegramMessageThreadId: number | null
  createdAtUtc: string | null
  lastActivityUtc: string | null
  clanMemberCount: number       // всего в клане по CR (0 — API не ответил)
  members: OwnerMember[]
}

/* --- Биржа игроков --- */
export interface RecruitmentStatus {
  isActive: boolean
  note: string | null
}

export interface RecruitmentCandidate {
  playerTag: string
  name: string
  note: string | null
  telegramUserId: number
  totalFame: number
  weeksPlayed: number
  avgFamePerAttack: number
  updatedAtUtc: string
}

export interface RecruitmentCandidates {
  candidates: RecruitmentCandidate[]
}

/* --- Турниры Clanify --- */
export type TournamentStatus = 'registrationOpen' | 'bracketReady' | 'inProgress' | 'completed' | 'cancelled'
export type TournamentParticipantStatus = 'active' | 'eliminated' | 'withdrawn'
export type TournamentMatchStatus = 'pending' | 'ready' | 'bye' | 'completed'

/** Формат турнира: одиночный или парный (команда из двоих). */
export type TournamentMode = 'solo' | 'duo'

export interface TournamentSummary {
  id: number
  name: string
  status: TournamentStatus
  mode: TournamentMode
  startsAtUtc: string | null
  bestOf: number
  /** Формат финала, если он отличается от остальных матчей; null — такой же. */
  finalBestOf: number | null
  maxParticipants: number
  participantCount: number
  creatorName: string
  createdAtUtc: string
  /** Чемпион; null — турнир не завершён. */
  championName: string | null
  completedAtUtc: string | null
}

export interface TournamentParticipant {
  id: number
  playerTag: string
  playerName: string
  /** Название команды в парном турнире; null — одиночный. */
  teamName: string | null
  partnerPlayerTag: string | null
  partnerPlayerName: string | null
  seed: number
  status: TournamentParticipantStatus
  finalPlacement: number | null
  /** Команда того, кто смотрит: в сетке её подсвечиваем. */
  isMe: boolean
}

/* --- Игровые турниры (отслеживание турнира CR по тегу) --- */
export interface GameTournamentMember {
  rank: number
  name: string
  score: number
  clanName: string | null
}

export interface GameTournamentLive {
  name: string
  description: string | null
  status: string                 // IN_PREPARATION | IN_PROGRESS | ENDED | UNKNOWN
  capacity: number
  maxCapacity: number
  levelCap: number
  firstPlaceCardPrize: number
  gameMode: string | null
  startsInSeconds: number | null
  endsInSeconds: number | null
  members: GameTournamentMember[]
}

export interface GameTournament {
  id: number
  tournamentTag: string
  password: string | null
  creatorName: string
  isCreator: boolean
  live: GameTournamentLive | null
}

export interface TournamentMatch {
  id: number
  round: number
  slotIndex: number
  participantA: TournamentParticipant | null
  participantB: TournamentParticipant | null
  scoreA: number
  scoreB: number
  winner: TournamentParticipant | null
  status: TournamentMatchStatus
  /** Счёт проставил бот по логу, а не организатор руками. */
  autoResolved: boolean
  nextMatchId: number | null
}

export interface Tournament {
  id: number
  name: string
  description: string | null
  prizeInfo: string | null
  /** null — организатор ещё не создал клан под турнир. */
  clanInviteLink: string | null
  creatorName: string
  bestOf: number
  /** Формат финала, если он отличается от остальных матчей; null — такой же. */
  finalBestOf: number | null
  minParticipants: number
  maxParticipants: number
  status: TournamentStatus
  mode: TournamentMode
  startsAtUtc: string | null
  createdAtUtc: string
  isCreator: boolean
  isParticipant: boolean
  canJoin: boolean
  /** Бот сам закрывает матчи по боевому логу участников. */
  autoResults: boolean
  /** Объявлять результаты матчей в чат клана организатора. */
  announceResults: boolean
  participants: TournamentParticipant[]
  matches: TournamentMatch[]
}

export interface PlayerTournamentHistory {
  tournamentId: number
  tournamentName: string
  status: TournamentStatus
  finalPlacement: number | null
  participantCount: number
  createdAtUtc: string
}

/* --- Подбор колод под коллекцию игрока --- */

export type CardRarity = 'common' | 'rare' | 'epic' | 'legendary' | 'champion'

export interface DeckCard {
  name: string
  level: number            // 0 — карта ещё не открыта
  maxLevel: number
  elixirCost: number
  rarity: CardRarity
  iconUrl: string
  owned: boolean
  evoUnlocked: boolean     // игрок открыл эволюцию этой карты
  hasEvo: boolean          // у карты вообще есть эволюция
  evoIconUrl: string | null
}

export interface DeckRarity {
  rarity: CardRarity
  count: number
}

export interface DeckSuggestion {
  id: string
  name: string
  archetype: string
  note: string
  cards: DeckCard[]
  ownedCount: number
  missing: string[]
  avgLevel: number
  avgElixir: number
  cycleCost: number        // 4 самые дешёвые карты — скорость прокрутки колоды
  levelsToMax: number      // сколько уровней ещё качать по открытым картам
  rarity: DeckRarity[]
  maxedCount: number
  evoUnlocked: number
  evoAvailable: number
  readiness: number        // 0..100
  verdict: string
  /** Ссылка «открыть колоду в игре»; null — у какой-то карты нет id в справочнике. */
  copyLink: string | null
}

/** Колода игрока мирового топа, примеренная на твою коллекцию. */
export interface TopDeck {
  playerName: string
  rank: number
  trophies: number
  clanName: string | null
  deck: DeckSuggestion
}

export interface DeckSuggestions {
  playerTag: string
  maxCardLevel: number
  baseUpdated: string      // «по состоянию на» — база правится вручную
  baseSource: string
  baseSize: number       // колод, прошедших сверку со справочником карт
  baseSkipped: number    // отброшено из-за незнакомого имени карты
  ready: DeckSuggestion[]  // можно собрать прямо сейчас
  almost: DeckSuggestion[] // не хватает 1–2 карт
  top: TopDeck[]           // что играют лучшие в мире прямо сейчас
}

/* --- Витрина клана, который ещё не подключён к боту --- */

export interface ClanMemberRow {
  playerTag: string
  name: string
  role: string          // leader | coLeader | elder | member
  trophies: number
  donations: number
}

export interface ClanOverview {
  clanTag: string
  clanName: string | null
  connected: boolean
  warTrophies: number
  memberCount: number | null
  countryName: string | null
  countryRank: number | null
  globalRank: number | null
  countryTop: RankedClanRow[]
  description: string | null
  type: string | null     // open | inviteOnly | closed
  clanScore: number
  requiredTrophies: number
  donationsPerWeek: number
  members: ClanMemberRow[] | null
}

/** Недавний поиск. Хранится только на устройстве, в localStorage. */
export interface SearchHistoryItem {
  kind: 'player' | 'clan'
  tag: string
  name: string
  at: number            // отметка времени последнего открытия
}

export interface LinkedPlayer {
  playerTag: string
  name: string
}

/**
 * Ключи фонов — совпадают с файлами в public/bg.
 *
 * 'sky' в наборы не входит: это оформление самого подиума Аллеи, когда ни у кого
 * из тройки нет своего фона. Выбрать его нельзя, поэтому в playerBackgrounds и
 * clanBackgrounds его не будет — но как значение он существует.
 */
export type BackgroundKey =
  | 'sky' | 'arena' | 'sunset' | 'night' | 'ice' | 'lava'
  | 'kingdomSun' | 'kingdom' | 'kingdom2' | 'kingdomFire'

/** Вкладки нижней панели. Состав задаёт владелец из админки. */
export type AppTab = 'clan' | 'me' | 'hall' | 'tournament' | 'search' | 'more' | 'challenge' | 'duel'

export interface AppConfig {
  botUsername: string
  tabs: AppTab[]
  /** Наборы разные: широкие сцены игроку в строку, вертикальные виды клану в блок. */
  playerBackgrounds: BackgroundKey[]
  clanBackgrounds: BackgroundKey[]
  isSponsor: boolean
  sponsorUntil: string | null
  myBackground: BackgroundKey | null
  myClanBackground: BackgroundKey | null
  /** Кому писать за спонсорством. Пусто — кнопку не показываем. */
  sponsorContact: string
  /** Цена в звёздах Telegram. 0 — продажа выключена, остаётся кнопка «написать». */
  sponsorPriceStars: number
  /** На сколько дней продаётся. */
  sponsorDays: number
}

/** Условия продажи и журнал оплат — для панели владельца. */
/** Строка воронки: рекламная кампания, код из ссылки с опечаткой или рефералы. */
export interface CampaignFunnel {
  source: string
  /** null — строка рефералов. */
  code: string | null
  name: string
  /** false — кампании с таким кодом нет: ссылка с опечаткой. */
  known: boolean
  createdAtUtc: string | null
  started: number
  linked: number
  clansConnected: number
  payers: number
  stars: number
}

/** Один шаг последней оплаты: когда, по какому счёту и чем кончился. */
export interface PaymentTraceEntry {
  atUtc: string
  payload: string
  chargeId: string | null
  outcome: string
}

export interface SponsorSales {
  stars: number
  days: number
  /** «Можно списывать?» — последний запрос. null — ни одного не приходило. */
  lastCheckout: PaymentTraceEntry | null
  /** «Списано» — последнее подтверждение и что с ним сделали. */
  lastPaid: PaymentTraceEntry | null
  totalStars: number
  payments: {
    playerTag: string
    stars: number
    days: number
    paidAtUtc: string
    telegramChargeId: string
    /** sponsor / plus */
    kind: string
    refundedAtUtc: string | null
  }[]
}

export interface HallPlayer {
  rank: number
  playerTag: string
  name: string
  clanName: string
  clanTag: string | null
  seasonFame: number
  weeksPlayed: number
  badgeKey: string | null
  badgeLevel: number
  isSponsor: boolean
  backgroundKey: BackgroundKey | null
}

export interface HallClan {
  rank: number
  clanId: number
  clanTag: string
  clanName: string
  seasonFame: number
  weeksPlayed: number
  sponsorCount: number
  backgroundKey: BackgroundKey | null
}

export interface HallOfFame {
  seasonId: number
  clansCounted: number
  totalPlayers: number
  /** Своя строка, даже если далеко за сотней. null — игрок не в зачёте. */
  me: HallPlayer | null
  players: HallPlayer[]
  clans: HallClan[]
}

/** Оформление страницы клана. plain доступен всем, остальные — клану со спонсором. */
export type ClanDesignKey =
  | 'plain' | 'royal' | 'gold' | 'neon' | 'stone' | 'blood' | 'frost'

/** Слава за одну военную неделю сезона — точка на маленьком графике страницы. */
export interface PageWeek {
  sectionIndex: number
  fame: number
}

export interface ClanPageMember {
  rank: number
  /** Место этого игрока на Аллее целиком, а не только внутри клана. */
  hallRank: number
  playerTag: string
  name: string
  seasonFame: number
  weeksPlayed: number
  badgeKey: string | null
  badgeLevel: number
  isSponsor: boolean
  backgroundKey: BackgroundKey | null
}

export interface ClanPage {
  clanId: number
  clanTag: string
  clanName: string
  motto: string | null
  /** 0 — клан ещё не попал в зачёт сезона. */
  rank: number
  clansCounted: number
  seasonId: number
  seasonFame: number
  weeksPlayed: number
  membersInSeason: number
  sponsorCount: number
  backgroundKey: BackgroundKey | null
  designKey: ClanDesignKey
  acceptsMail: boolean
  isMine: boolean
  canEdit: boolean
  /** Что зритель вправе выбрать. Пусто — настраивать не его дело. */
  availableDesigns: ClanDesignKey[]
  members: ClanPageMember[]
  weeks: PageWeek[]
}

export interface PlayerPageBadge {
  key: string
  level: number
  value: number
}

export interface PlayerPage {
  rank: number
  totalPlayers: number
  seasonId: number
  playerTag: string
  name: string
  clanName: string
  clanTag: string | null
  /** null — клана нет в боте, переходить некуда. */
  clanId: number | null
  seasonFame: number
  weeksPlayed: number
  bestWeekFame: number
  isSponsor: boolean
  backgroundKey: BackgroundKey | null
  showcaseKey: string | null
  showcaseLevel: number
  isMe: boolean
  /** Оформление берётся у клана: своего у игрока нет. */
  designKey: ClanDesignKey
  badges: PlayerPageBadge[]
  weeks: PageWeek[]
}

/** Состояние снимков мирового топа. Видно только владельцу и модератору. */
export interface TopStatus {
  /** Самый свежий день со снимком, 'YYYY-MM-DD'. null — снимков нет вообще. */
  latestDay: string | null
  daysStored: number
  /** Когда воркер (или панель) последний раз пытался собрать. null — попыток не было. */
  lastAttemptAtUtc: string | null
  lastRows: number
  /** Почему не вышло. null — вышло. */
  lastProblem: string | null
  /** Итог сбора меты по боям: сколько боёв и колод или почему нет. */
  lastMeta?: string | null
  /** Сбор из панели идёт прямо сейчас. */
  running?: boolean
}

export interface OwnerSponsor {
  playerTag: string
  name: string
  clanName: string | null
  until: string
  background: BackgroundKey | null
  daysLeft: number
}

/* --- Трекер боёв --- */

export interface TrackerState {
  /** Трекер открыт этому человеку (закрытый тест). */
  available: boolean
  enabled: boolean
  dmBlocked: boolean
  mutedToday: boolean
}

export interface MatchArch {
  key: string
  /** Уже с «колода с «X»» для колод вне архетипов. */
  label: string
  card: MetaCard | null
}

export type MatchMode = 'ladder' | 'pol' | 'war' | 'trail' | 'tourney' | 'other'

export interface MatchRow {
  id: number
  timeUtc: string
  mode: MatchMode
  /** 1 победа, 0 ничья, −1 поражение. */
  result: number
  crownsFor: number
  crownsAgainst: number
  trophyChange: number | null
  oppName: string | null
  arch: MatchArch | null
  keyCards: MetaCard[]
  verdict: string | null
  levelGap: number | null
  /** Как этот матчап играет в топ-500; null — боёв мало или меты нет. */
  topPct?: number | null
  topGames?: number
}

export interface MatchSession {
  startUtc: string
  endUtc: string
  wins: number
  losses: number
  draws: number
  trophies: number
  tilt: boolean
  /** W / L / D по порядку. */
  strip: string
  matches: MatchRow[]
}

export interface ArchetypeRow {
  arch: MatchArch
  games: number
  wins: number
  losses: number
  deltaPp: number
}

export interface MatchHistory {
  tracker: TrackerState
  unlocked: boolean
  historyDays: number
  aggregates: {
    archetypes: ArchetypeRow[]
    archetypesLocked: number
    levels: { lossAvg: number | null; winAvg: number | null; underWinPct: number | null; evenWinPct: number | null } | null
    /** 7 дней × 4 части суток: weekday 0 — понедельник, part 0 — ночь … 3 — вечер. */
    heat?: { weekday: number; part: number; games: number; wins: number }[] | null
    basePct?: number
  } | null
  sessions: MatchSession[]
  lockedOlder: number
  nextBefore: string | null
}

export interface MatchCard {
  cardId: number
  name: string
  iconUrl: string
  /** Игровой уровень; 0 — неизвестен. */
  level: number
  /** 0 обычная, 1 эволюция, 2 герой. */
  form: number
  elixir: number
}

export interface MatchSide {
  deck: MatchCard[]
  towerTroop: MatchCard | null
  kingHp: number | null
  princessHp: number[] | null
  leak: number | null
  trophies: number | null
  copyLink: string | null
}

export interface MatchReport {
  id: number
  timeUtc: string
  mode: MatchMode
  modeName: string | null
  result: number
  crownsFor: number
  crownsAgainst: number
  trophyChange: number | null
  opp: { name: string | null; tag: string | null; clan: string | null; trophies: number | null; diff: number | null; globalRank: number | null }
  arch: MatchArch | null
  oppAvgElixir: number | null
  me: MatchSide
  them: MatchSide
  levels: { myAvg: number; oppAvg: number; gap: number; lowestMine: MatchCard | null } | null
  leak: { mine: number | null; theirs: number | null; usual: number | null } | null
  verdictCode: string | null
  verdict: string | null
  session: { startUtc: string; index: number; count: number; wins: number; losses: number; draws: number; trophies: number; strip: string }
  plus: { vsArch: string | null; betterDeck: string | null; deckVsArchWins: number | null; deckVsArchLosses: number | null } | null
  /** Строки Плюса открыты бесплатной подсказкой дня. */
  hint: boolean
  lockedCount: number
  hasDetail: boolean
  unlocked: boolean
  matchup?: Matchup | null
}

export interface MatchupTier {
  /** exact | seven | six | wincon4 | wincon */
  key: string
  wins: number
  draws: number
  losses: number
  games: number
  winPercent: number
  reliability: 'reliable' | 'adequate' | 'low'
}

export interface Matchup {
  top: MatchupTier[] | null
  topBattles: number
  headline: string | null
  own: MatchupTier[] | null
  ownLocked: boolean
  me: { avgElixir: number; cycle: number } | null
  them: { avgElixir: number; cycle: number } | null
}

/* --- Уикенд-челлендж --- */

export interface ChallengeRow {
  rank: number
  name: string
  tag: string
  tickets: number
  wins: number
  losses: number
  /** Текущая серия побед — сколько до бонусного билета. */
  streak: number
  bestStreak: number
  isMe: boolean
}

export interface Challenge {
  event: { id: string; title: string | null; prize: string | null; startUtc: string; endUtc: string; status: 'upcoming' | 'live' | 'ended'; giftPlus?: boolean }
  linked: boolean
  joined: boolean
  me: ChallengeRow | null
  leaders: ChallengeRow[]
  participants: number
  updatedUtc: string
}

export interface OwnerChallenge {
  id: string
  title: string | null
  prize: string | null
  startUtc: string
  endUtc: string
  status: 'upcoming' | 'live' | 'ended'
  participants?: number
  giftPlus?: boolean
  /** Участники других версий события — их можно вернуть. */
  others?: { id: string; participants: number; lastJoinedUtc: string }[]
}

/** Поиск игрока в панели владельца: кто в Telegram за игровым тегом. */
export interface OwnerFoundPlayer {
  playerTag: string
  name: string
  clanName: string | null
  telegramUserId: number | null
  telegramUsername: string | null
  plusUntil: string | null
  sponsorUntil: string | null
  dmBlocked: boolean | null
}

/* ---------- Лига дуэлей 1×1 ---------- */

export type DuelLeagueKey = 'bronze' | 'silver' | 'gold' | 'diamond' | 'master' | 'legend'

export interface DuelProfileView {
  name: string
  tag: string
  rating: number
  peak: number
  league: DuelLeagueKey
  leagueIndex: number
  /** 3, 2, 1 (I — старший); 0 у Легенды. */
  division: number
  /** С каких кубков следующий ранг; null — уже Легенда. */
  nextFloor: number | null
  floor: number
  games: number
  wins: number
  losses: number
  rank: number
  hasLink: boolean
}

export interface DuelRow {
  id: number
  state: 'active' | 'finished' | 'cancelled' | 'expired'
  bestOf: number
  aName: string
  aTag: string
  bName: string
  bTag: string
  scoreA: number
  scoreB: number
  deltaA: number
  deltaB: number
  rated: boolean
  acceptedUtc: string
  finishedUtc: string | null
}

export interface DuelTopRow {
  rank: number
  name: string
  tag: string
  rating: number
  league: DuelLeagueKey
  division: number
  wins: number
  losses: number
  me: boolean
}

export interface DuelLeague {
  linked: boolean
  me: DuelProfileView | null
  active: DuelRow | null
  mine: DuelRow[]
  top: DuelTopRow[]
  recent: DuelRow[]
  players: number
  floors: number[]
  leagues: DuelLeagueKey[]
}
