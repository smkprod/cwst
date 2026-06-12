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
  role?: string              // "Лидер" | "Соруководитель" | "Старейшина" | undefined (рядовой)
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
}

export type Plan = 'free' | 'pro'

/** Один клан в таблице гонки недели. */
export interface RaceClan {
  tag: string
  name: string
  position: number           // 1..5
  fame: number               // медали за неделю
  periodPoints: number       // медали текущего дня
  projectedFame: number      // прогноз медалей к концу недели
  avgFamePerAttack: number
  decksUsedToday: number
  maxDecksToday: number
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
}

export interface PlayerHistory {
  playerTag: string
  royaleApiUrl: string
  weeks: PlayerWeekHistory[]
}

/** Журнал прошлых войн (официальный riverracelog): места кланов и очки. */
export interface WarLogClan {
  rank: number               // 1..5
  name: string
  fame: number               // медали клана за неделю
  trophyChange: number       // +/- КВ-трофеи по итогам
  isOurClan: boolean
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
export interface OwnerClan {
  id: number
  clanTag: string
  name: string
  plan: Plan
  planExpiresAtUtc: string | null
  linkedPlayers: number
}
