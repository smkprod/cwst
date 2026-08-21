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
  consecutiveWars: number    // Pro: недель подряд участвовал (0 = Free или не участвовал)
  role?: string              // "leader" | "coLeader" | "elder" | undefined (рядовой)
  trophies: number           // кубки игрока (0 — состав клана не отдался)
  dnaLabel?: string          // Pro: архетип ("Тащер 💪" и т.п.), undefined — мало данных/Free
  reliabilityScore: number   // Pro: надёжность 0..100 (0 — нет данных/Free)
}

/** Pro-аналитика: шанс победы и здоровье клана. */
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

export type Plan = 'free' | 'pro'

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
  name: string
  fame: number
  decksUsed: number
}

/** Журнал прошлых войн (официальный riverracelog): места кланов и очки. */
export interface WarLogClan {
  rank: number               // 1..5
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
  plan: Plan
  stats: ClanStats
  forecast: ClanForecast | null   // null на Free-тарифе
  race: RaceClan[]                // ситуация в гонке (все кланы недели)
  players: PlayerStatus[]
  insights: ClanInsights | null   // Pro-аналитика (null на Free)
  warLog: WarLogWeek[]            // журнал прошлых войн (места кланов и очки)
  dayLogs: WarDayLog[]            // официальный по-дневный лог гонки (periodLogs из API)
  myPlayerTag?: string       // тег текущего пользователя (если /my/status)
  isAdmin?: boolean          // админ ли текущий пользователь в группе клана
  isClanLeader?: boolean     // leader или coLeader в CR-клане
  isOwner?: boolean          // владелец сервиса (видит панель ⚙️)
  reminderHoursBeforeEnd?: number // за сколько часов до конца дня шлём автонапоминания
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
  level: number          // 0 нет, 1 бронза, 2 серебро, 3 золото
  value: number
  nextAt: number | null  // порог следующего уровня, null = золото
  thresholds: number[]
}

export interface Achievements {
  playerTag: string
  badges: Achievement[]
  weeksAnalyzed: number
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
  season: MySeason | null    // null — Free или данных ещё нет
}

export interface NudgeResult {
  notifiedDm: number
  skippedCooldown: number
  taggableCount: number
  unlinkedCount: number
  postedToChat: boolean
}

/* --- История войн (Pro) --- */
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

/* --- Сезонный зачёт (Pro) --- */
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

/* --- Разбивка сезона по неделям (Pro): каждая война + общий зачёт --- */
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
  royaleApiUrl: string
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

/* --- Панель владельца --- */
export interface OwnerStats {
  // Кланы
  totalClans: number
  proClans: number
  freeClans: number
  chatsWithBot: number
  activeClans7d: number
  silentClans: number
  // Пользователи
  totalLinkedUsers: number
  usersWithClan: number
  usersWithoutClan: number
  usersWithUsername: number
  invitedUsers: number
  // Рост (только по записям с известной датой)
  newClans7d: number
  newClans30d: number
  newUsers7d: number
  newUsers30d: number
  clansWithKnownDate: number
  usersWithKnownDate: number
  // Pro
  proExpiring7d: number
  proExpired: number
  proForever: number
  // Вовлечённость
  respects7d: number
  avgLinkedPerClan: number
}

export type NotifyChannel = 'dm' | 'chat' | 'both'

export interface NotificationSettings {
  reminderHoursBeforeEnd: number
  remindersEnabled: boolean
  remindersChannel: NotifyChannel
  warStartEnabled: boolean
  warStartChannel: NotifyChannel
  finalCallEnabled: boolean
  dailyReportEnabled: boolean
  warEndMinuteUtc: number | null   // во сколько заканчивается КВ (минуты от 00:00 UTC), null = 10:00 по умолчанию
  perfectDayEnabled: boolean       // поздравление «900 за день» в чат
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
  plan: Plan
  planExpiresAtUtc: string | null
  daysLeft: number | null       // сколько дней Pro осталось; null — бессрочно/Free
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
  plan: Plan
  planExpiresAtUtc: string | null
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

export interface TournamentSummary {
  id: number
  name: string
  status: TournamentStatus
  bestOf: number
  maxParticipants: number
  participantCount: number
  creatorName: string
  createdAtUtc: string
}

export interface TournamentParticipant {
  id: number
  playerTag: string
  playerName: string
  seed: number
  status: TournamentParticipantStatus
  finalPlacement: number | null
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
  nextMatchId: number | null
}

export interface Tournament {
  id: number
  name: string
  description: string | null
  prizeInfo: string | null
  clanInviteLink: string
  creatorName: string
  bestOf: number
  minParticipants: number
  maxParticipants: number
  status: TournamentStatus
  createdAtUtc: string
  isCreator: boolean
  isParticipant: boolean
  canJoin: boolean
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
