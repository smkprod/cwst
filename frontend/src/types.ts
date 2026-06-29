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
  avgFamePerAttack: number   // todayFame / decksUsedToday
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

export interface PlayerProfile {
  playerTag: string
  name: string
  expLevel: number
  trophies: number
  clanWarTrophies: number
  clanName: string | null
  clanTag: string | null
  arenaName: string | null
  cards: PlayerCard[]
  weeksPlayed: number
  totalFame: number
  avgFamePerAttack: number
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
  totalClans: number
  proClans: number
  totalLinkedUsers: number
  usersWithClan: number
  usersWithoutClan: number
  chatsWithBot: number
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
  linkedPlayers: number
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
