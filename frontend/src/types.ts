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
  fame: number
  periodPoints: number       // очки текущего дня
  projectedFame: number      // прогноз славы к концу недели
  avgFamePerAttack: number
  decksUsedToday: number
  maxDecksToday: number
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
  myPlayerTag?: string       // тег текущего пользователя (если /my/status)
  isAdmin?: boolean          // админ ли текущий пользователь в группе клана
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

/* --- Панель владельца --- */
export interface OwnerClan {
  id: number
  clanTag: string
  name: string
  plan: Plan
  planExpiresAtUtc: string | null
  linkedPlayers: number
}
