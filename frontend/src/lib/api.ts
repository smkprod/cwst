import { initData } from './telegram'
import type { AppConfig, AppTab, BackgroundKey, ClanDesignKey, ClanPage, PlayerPage, HallOfFame, OwnerSponsor, Moderator, ServiceIdentity, ServicePermission, BroadcastTarget, TopStatus, SponsorSales, CampaignFunnel, ClanDiscipline, ClanHistory, ClanOverview, ClanRanking, ClanStatus, ClanWarLog, DeckSuggestions, GameTournament, GlobalTop, LinkedPlayer, MyStats, NotificationSettings, NudgeResult, OwnerClan, OwnerClanDetail, OwnerStats, PlayerHistory, PlayerProfile, PlayerTournamentHistory, RaceScout, TournamentMode, RecruitmentCandidates, RecruitmentStatus, Achievements, WhatsNew, RespectStatus, SeasonArchive, SeasonBreakdown, SeasonStats, TopMeta, MetaDecks, BattleAnalysis, PlayerSheet, PlusStatus, OwnerPlus, TiltProfile, Challenge, OwnerChallenge, TrackerState, MatchHistory, MatchReport, TopPlayerRow, TopPlayerDetail, Tournament, TournamentSummary, WarJournal } from '../types'

// Если мы на Render (production), BASE должен быть пустой строкой '', чтобы запросы шли на тот же домен.
// Для локальной разработки (Development) оставляем localhost:5000.
const BASE = import.meta.env.DEV 
  ? (import.meta.env.VITE_API_URL ?? 'http://localhost:5000') 
  : '';

/**
 * Без таймаута зависший запрос ждёт бесконечно: мобильная сеть умеет «принять»
 * соединение и замолчать, и тогда экран остаётся ни живым ни мёртвым.
 * Обрываем сами — вызывающий код воспримет это как обычный сбой и повторит.
 */
const REQUEST_TIMEOUT_MS = 15_000

/**
 * Клан, в который админ сервиса зашёл из панели.
 *
 * Живёт здесь, а не в пропсах экранов: заголовок должен уходить со ВСЕМИ запросами
 * «мой клан», а их полтора десятка и они разбросаны по всем вкладкам. Протащить
 * clanId в каждую значило бы рано или поздно забыть одну, и она молча показывала
 * бы свой клан посреди чужого — расхождение, которое почти невозможно заметить.
 *
 * sessionStorage, а не localStorage: заход в чужой клан не должен переживать
 * закрытие приложения и встречать потом как «почему у меня чужая война».
 */
const ADMIN_CLAN_KEY = 'adminClanId'

function readAdminClan(): number | null {
  try {
    const raw = sessionStorage.getItem(ADMIN_CLAN_KEY)
    const id = raw ? Number(raw) : NaN
    return Number.isFinite(id) && id > 0 ? id : null
  } catch {
    // Приватный режим и заблокированные куки роняют доступ к хранилищу.
    return null
  }
}

export const adminClan = {
  get: readAdminClan,
  enter(clanId: number) {
    try { sessionStorage.setItem(ADMIN_CLAN_KEY, String(clanId)) } catch { /* см. readAdminClan */ }
  },
  leave() {
    try { sessionStorage.removeItem(ADMIN_CLAN_KEY) } catch { /* см. readAdminClan */ }
  },
}

async function request<T>(path: string, init?: RequestInit, timeoutMs = REQUEST_TIMEOUT_MS): Promise<T> {
  // КРИТИЧЕСКИЙ ФИКС: Достаем свежайший initData из window прямо в секунду отправки запроса.
  // Теперь заголовок больше никогда не уйдет на сервер пустым.
  const liveInitData = window.Telegram?.WebApp?.initData ?? '';

  const ctrl = new AbortController()
  const timer = setTimeout(() => ctrl.abort(), timeoutMs)

  // Ручки самой панели шлём всегда от своего имени: иначе, зайдя в чужой клан,
  // ты перестал бы видеть в панели список кланов и не смог бы из него выйти.
  const acting = path.startsWith('/api/owner') ? null : readAdminClan()

  let res: Response
  try {
    res = await fetch(`${BASE}${path}`, {
      ...init,
      signal: ctrl.signal,
      headers: {
        'X-Telegram-Init-Data': liveInitData,
        ...(acting ? { 'X-Admin-Clan': String(acting) } : {}),
        ...init?.headers
      },
    })
  } finally {
    clearTimeout(timer)
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(res.status, body.error ?? 'unknown', body.message)
  }
  return res.json()
}

export class ApiError extends Error {
  constructor(public status: number, public code: string, message?: string) {
    super(message ?? code)
  }
}

export const api = {
  /** Что фронту нужно знать о боте в рантайме (юзернейм для ссылок). */
  getAppConfig: () => request<AppConfig>('/api/app/config'),
  /** Аллея славы: топ игроков и кланов сервиса за сезон. */
  getHallOfFame: () => request<HallOfFame>('/api/hall'),
  setShowcaseBadge: (key: string | null) =>
    request<{ key: string | null; level: number }>('/api/players/me/showcase', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ key }),
    }),
  setMyBackground: (key: BackgroundKey | null, scope: 'player' | 'clan' = 'player') =>
    request<{ key: BackgroundKey | null }>('/api/players/me/background', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ key, scope }),
    }),
  /** Страница клана с Аллеи — открывается и с подиума, и из списка. */
  getClanPage: (clanId: number) => request<ClanPage>(`/api/hall/clans/${clanId}`),
  /** Страница игрока. Решётку в теге срезаем: она ломает путь. */
  getPlayerPage: (playerTag: string) =>
    request<PlayerPage>(`/api/hall/players/${encodeURIComponent(playerTag.replace('#', ''))}`),
  /** Оформление и девиз страницы своего клана. null в поле — «не трогать». */
  setClanPage: (clanId: number, body: { designKey?: ClanDesignKey; motto?: string }) =>
    request<{ ok: boolean }>(`/api/hall/clans/${clanId}/page`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  /** Счёт на спонсорство в звёздах — ссылка для Telegram.WebApp.openInvoice. */
  createSponsorInvoice: () =>
    request<{ link: string }>('/api/sponsor/invoice', { method: 'POST' }),
  ownerGetSponsorSales: () => request<SponsorSales>('/api/owner/sponsor/sales'),
  ownerGetCampaigns: () => request<CampaignFunnel[]>('/api/owner/campaigns'),
  ownerCreateCampaign: (code: string, name: string) =>
    request<{ code: string; name: string }>('/api/owner/campaigns', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code, name }),
    }),
  ownerSetSponsorSales: (stars: number, days: number) =>
    request<{ stars: number; days: number }>('/api/owner/sponsor/sales', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ stars, days }),
    }),
  /** Написать другому клану от имени своего. kind: обычное сообщение или вызов. */
  sendClanMessage: (clanId: number, text: string, kind: 'message' | 'challenge') =>
    request<{ ok: boolean }>(`/api/hall/clans/${clanId}/message`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text, kind }),
    }),
  ownerGrantSponsor: (playerTag: string, days: number) =>
    request<{ playerTag: string; name: string; until: string | null }>('/api/owner/sponsor', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerTag, days }),
    }),
  ownerGetSponsors: () => request<OwnerSponsor[]>('/api/owner/sponsors'),
  ownerSetTabs: (tabs: AppTab[]) =>
    request<{ tabs: AppTab[] }>('/api/owner/tabs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tabs }),
    }),
  getMyClanStatus: () => request<ClanStatus>('/api/clans/my/status'),
  getClanStatus: (tag: string) =>
    request<ClanStatus>(`/api/clans/${encodeURIComponent(tag.replace('#', ''))}/status`),
  getMyStats: () => request<MyStats>('/api/players/me/stats'),
  getPlayerHistory: (tag: string) =>
    request<PlayerHistory>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/history`),
  getPlayerProfile: (tag: string) =>
    request<PlayerProfile>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/profile`),
  getMe: () => request<LinkedPlayer>('/api/players/me'),
  /** «Clanify Плюс»: статус, цены, оповещения. */
  getPlus: () => request<PlusStatus>('/api/plus'),
  /** Счёт на пропуск Плюса (7 или 30 дней) — ссылка для openInvoice. */
  /** С тегом получателя — подарок; без — себе (или «попросить в подарок»: ссылку пересылают). */
  createPlusInvoice: (days: number, recipientTag?: string) =>
    request<{ link: string }>('/api/plus/invoice', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ days, recipientTag: recipientTag ?? null }),
    }),
  /** Бесплатная подарочная неделя спонсора. */
  giftFreePlus: (recipientTag: string) =>
    request<{ recipient: string; until: string; freeGiftsLeft: number }>('/api/plus/gift-free', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ recipientTag }),
    }),
  getTilt: () => request<TiltProfile>('/api/plus/tilt'),
  getChallenge: () => request<Challenge>('/api/challenge'),
  joinChallenge: () => request<Challenge>('/api/challenge/join', { method: 'POST' }),
  ownerGetChallenge: () => request<OwnerChallenge>('/api/owner/challenge'),
  ownerGiftChallengePlus: () => request<{ granted: number }>('/api/owner/challenge/gift-plus', { method: 'POST' }),
  ownerRestoreChallenge: (eventId: string) =>
    request<OwnerChallenge>('/api/owner/challenge/restore', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ eventId }),
    }),
  ownerSetChallenge: (c: { title: string | null; prize: string | null; startUtc: string; endUtc: string; newEvent?: boolean }) =>
    request<OwnerChallenge>('/api/owner/challenge', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(c),
    }),
  getTracker: () => request<TrackerState>('/api/players/me/tracker'),
  dmAllowed: () => request<{ ok: boolean }>('/api/players/me/dm-allowed', { method: 'POST' }),
  setTracker: (enabled: boolean, tz: number, lang: string) =>
    request<TrackerState>('/api/players/me/tracker', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled, tz, lang }),
    }),
  getMatches: (q: { before?: string | null; result?: string; mode?: string; arch?: string | null; lang: string }) => {
    const p = new URLSearchParams({ lang: q.lang, tz: String(-new Date().getTimezoneOffset()) })
    if (q.before) p.set('before', q.before)
    if (q.result && q.result !== 'all') p.set('result', q.result)
    if (q.mode && q.mode !== 'all') p.set('mode', q.mode)
    if (q.arch) p.set('arch', q.arch)
    return request<MatchHistory>(`/api/players/me/matches?${p}`)
  },
  getMatch: (id: number, lang: string) => request<MatchReport>(`/api/players/me/matches/${id}?lang=${lang}`),
  ownerSetTracker: (dm: boolean, beta: string) =>
    request<{ dm: boolean; beta: string }>('/api/owner/tracker/settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ dm, beta }),
    }),
  /** Настройки «Стоп-тильта»: передаются только меняемые поля. dailyLossLimit 0 — выключить. */
  setTiltPrefs: (prefs: { enabled?: boolean; lossThreshold?: number; dailyLossLimit?: number; quietHours?: boolean }) =>
    request<{ tiltAlerts: boolean | null }>('/api/plus/alerts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(prefs),
    }),
  ownerGetPlus: () => request<OwnerPlus>('/api/owner/plus'),
  ownerSetPlus: (paywall: boolean, price7: number, price30: number) =>
    request<{ paywall: boolean }>('/api/owner/plus/settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ paywall, price7, price30 }),
    }),
  ownerGrantPlus: (playerTag: string, days: number) =>
    request<{ playerTag: string; name: string; until: string }>('/api/owner/plus/grant', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerTag, days }),
    }),
  ownerRefund: (chargeId: string) =>
    request<{ refunded: boolean }>('/api/owner/payments/refund', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ chargeId }),
    }),
  /** Единая карточка игрока — одна на весь бот. */
  getPlayerSheet: (tag: string) =>
    request<PlayerSheet>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/sheet`),
  /** Личный разбор боёв. tz — смещение местного времени от UTC в минутах. */
  getMyBattles: (lang?: string) =>
    request<BattleAnalysis>(`/api/players/me/battles?tz=${-new Date().getTimezoneOffset()}${lang ? `&lang=${lang}` : ''}`),
  getPlayerDecks: (tag: string) =>
    request<DeckSuggestions>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/decks`),
  getClanOverview: (tag: string) =>
    request<ClanOverview>(`/api/clans/${encodeURIComponent(tag.replace('#', ''))}/overview`),
  getClanWarLog: (tag: string) =>
    request<ClanWarLog>(`/api/clans/${encodeURIComponent(tag.replace('#', ''))}/warlog`),
  nudgeSlackers: () => request<NudgeResult>('/api/clans/my/nudge', { method: 'POST' }),
  setReminderHours: (hoursBeforeEnd: number) =>
    request<{ ok: boolean; reminderHoursBeforeEnd: number }>('/api/clans/my/reminder', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ hoursBeforeEnd }),
    }),
  getClanRanking: () => request<ClanRanking>('/api/clans/my/ranking'),
  getClanDiscipline: () => request<ClanDiscipline>('/api/clans/my/discipline'),
  getRaceScout: () => request<RaceScout>('/api/clans/my/scout'),
  getWarJournal: () => request<WarJournal>('/api/clans/my/war-journal'),
  getNotificationSettings: () =>
    request<NotificationSettings>('/api/clans/my/notification-settings'),
  setNotificationSettings: (s: NotificationSettings) =>
    request<NotificationSettings>('/api/clans/my/notification-settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(s),
    }),
  getMyClanHistory: (weeks = 8) => request<ClanHistory>(`/api/clans/my/history?weeks=${weeks}`),
  getMyClanSeason: () => request<SeasonStats>('/api/clans/my/season'),
  getSeasonBreakdown: () => request<SeasonBreakdown>('/api/clans/my/season-weeks'),
  getSeasonArchive: () => request<SeasonArchive>('/api/clans/my/season-archive'),
  getGlobalTop: () => request<GlobalTop>('/api/players/top'),

  // Мировой топ (снимки CR-рейтинга)
  // 204 — снимков ещё нет: копим. Пустое тело, поэтому не парсим.
  getTopMeta: async (): Promise<TopMeta | null> => {
    const res = await fetch(`${BASE}/api/top/meta`, {
      headers: { 'X-Telegram-Init-Data': window.Telegram?.WebApp?.initData ?? '' },
    })
    if (!res.ok || res.status === 204) return null
    return res.json().catch(() => null)
  },
  // 204 — меты по боям ещё нет. Пустое тело, поэтому не парсим.
  getMetaDecks: async (): Promise<MetaDecks | null> => {
    const res = await fetch(`${BASE}/api/top/decks`, {
      headers: { 'X-Telegram-Init-Data': window.Telegram?.WebApp?.initData ?? '' },
    })
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    if (res.status === 204) return null
    return res.json()
  },
  // 204 — ни в мете, ни в профилях топа колод с картой нет.
  getCardDecks: async (cardId: number): Promise<MetaDecks | null> => {
    const res = await fetch(`${BASE}/api/top/cards/${cardId}/decks`, {
      headers: { 'X-Telegram-Init-Data': window.Telegram?.WebApp?.initData ?? '' },
    })
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    if (res.status === 204) return null
    return res.json()
  },
  getTopPlayers: (skip = 0, take = 50) =>
    request<TopPlayerRow[]>(`/api/top/players?skip=${skip}&take=${take}`),
  getTopPlayerDetail: (tag: string) =>
    request<TopPlayerDetail>(`/api/top/players/${encodeURIComponent(tag.replace('#', ''))}`),
  getMyAchievements: () => request<Achievements>('/api/players/me/achievements'),
  getPlayerAchievements: (tag: string) =>
    request<Achievements>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/achievements`),
  // 204 (нет данных: не привязан / нет войны) — отдаём null, а не падаем на пустом теле
  getWhatsNew: async (): Promise<WhatsNew | null> => {
    const res = await fetch(`${BASE}/api/players/me/whats-new`, {
      headers: { 'X-Telegram-Init-Data': window.Telegram?.WebApp?.initData ?? '' },
    })
    if (!res.ok || res.status === 204) return null
    return res.json().catch(() => null)
  },
  getRespectStatus: () => request<RespectStatus>('/api/players/me/respect-status'),

  /** Ссылка-приглашение для непривязанного игрока (лидер/админ). */
  getClaimLink: (tag: string) =>
    request<{ link: string; playerTag: string; expiresUtc: string }>(
      `/api/clans/my/claim-link?tag=${encodeURIComponent(tag)}`),

  giveRespect: (tag: string) =>
    request<{ ok: boolean; total: number }>(
      `/api/players/${encodeURIComponent(tag.replace('#', ''))}/respect`, { method: 'POST' }),

  getRecruitmentStatus: () => request<RecruitmentStatus>('/api/recruitment/me'),
  applyRecruitment: (note: string) =>
    request<{ ok: boolean }>('/api/recruitment', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ note }),
    }),
  withdrawRecruitment: () =>
    request<{ ok: boolean }>('/api/recruitment', { method: 'DELETE' }),
  getRecruitmentCandidates: () =>
    request<RecruitmentCandidates>('/api/recruitment/candidates'),

  // Панель владельца
  /** Кто я для сервиса. Не требует ни привязанного тега, ни клана. */
  ownerMe: () => request<ServiceIdentity>('/api/owner/me'),
  ownerGetModerators: () => request<Moderator[]>('/api/owner/moderators'),
  ownerAddModerator: (username: string, note: string | undefined, permissions: ServicePermission[]) =>
    request<{ ok: boolean; username: string }>('/api/owner/moderators', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, note, permissions }),
    }),
  ownerSetModeratorPermissions: (id: number, permissions: ServicePermission[]) =>
    request<{ ok: boolean; permissions: ServicePermission[] }>(
      `/api/owner/moderators/${id}/permissions`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ permissions }),
      }),
  ownerRemoveModerator: (id: number) =>
    request<{ ok: boolean }>(`/api/owner/moderators/${id}`, { method: 'DELETE' }),
  ownerHarvestTop: () =>
    request<{ started: boolean; running: boolean }>('/api/owner/top/harvest', { method: 'POST' }),
  /** Что со снимками мирового топа — без запуска тысячи запросов к API игры. */
  ownerTopStatus: () => request<TopStatus>('/api/owner/top/status'),
  ownerGetStats: () => request<OwnerStats>('/api/owner/stats'),
  ownerGetClans: () => request<OwnerClan[]>('/api/owner/clans'),
  ownerGetClanDetail: (clanId: number) => request<OwnerClanDetail>(`/api/owner/clans/${clanId}`),
  ownerDeleteClan: (clanId: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}`, { method: 'DELETE' }),
  ownerBroadcast: (text: string, target: BroadcastTarget) =>
    request<{ started: boolean }>('/api/owner/broadcast', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text, target }),
    }),
  /** Рассылка со скринами: multipart, заголовок Content-Type браузер ставит сам. */
  ownerBroadcastMedia: (text: string, target: BroadcastTarget, photos: Blob[]) => {
    const form = new FormData()
    form.append('text', text)
    form.append('target', target)
    photos.forEach((p, i) => form.append('photos', p, `shot-${i + 1}.jpg`))
    return request<{ started: boolean; photos: number }>('/api/owner/broadcast/media', { method: 'POST', body: form }, 60_000)
  },

  // Турниры
  /** Привязать себя к игроку, не выходя из приложения. */
  linkMe: (tag: string) =>
    request<{ playerTag: string; name: string }>('/api/players/me/link', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tag }),
    }),

  getTournaments: () => request<TournamentSummary[]>('/api/tournaments'),
  /** Завершённые турниры с чемпионами — история. */
  getTournamentHistory: (limit = 20) =>
    request<TournamentSummary[]>(`/api/tournaments/history?limit=${limit}`),
  getTournament: (id: number) => request<Tournament>(`/api/tournaments/${id}`),
  createTournament: (req: {
    name: string; description?: string; prizeInfo?: string
    clanInviteLink?: string; bestOf: number; finalBestOf?: number | null
    minParticipants: number; maxParticipants: number
    mode?: TournamentMode; startsAtUtc?: string | null
  }) =>
    request<Tournament>('/api/tournaments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  /** Снять команду с турнира (только создатель). */
  removeTournamentParticipant: (id: number, participantId: number) =>
    request<Tournament>(`/api/tournaments/${id}/participants/${participantId}`, { method: 'DELETE' }),

  updateTournament: (id: number, req: {
    name: string; description?: string; prizeInfo?: string
    clanInviteLink?: string; bestOf: number; finalBestOf?: number | null
    minParticipants: number; maxParticipants: number
    startsAtUtc?: string | null
    autoResults?: boolean
    announceResults?: boolean
  }) =>
    request<Tournament>(`/api/tournaments/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  /** В парном турнире обязательны название команды и тег напарника. */
  joinTournament: (id: number, team?: { teamName: string; partnerTag: string }) =>
    request<Tournament>(`/api/tournaments/${id}/join`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(team ?? {}),
    }),
  leaveTournament: (id: number) =>
    request<Tournament>(`/api/tournaments/${id}/leave`, { method: 'POST' }),
  generateTournamentBracket: (id: number) =>
    request<Tournament>(`/api/tournaments/${id}/bracket`, { method: 'POST' }),
  startTournament: (id: number) =>
    request<Tournament>(`/api/tournaments/${id}/start`, { method: 'POST' }),
  finishTournament: (id: number) =>
    request<Tournament>(`/api/tournaments/${id}/finish`, { method: 'POST' }),
  setTournamentMatchResult: (id: number, matchId: number, scoreA: number, scoreB: number) =>
    request<Tournament>(`/api/tournaments/${id}/matches/${matchId}/result`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scoreA, scoreB }),
    }),
  cancelTournament: (id: number) =>
    request<{ ok: boolean }>(`/api/tournaments/${id}`, { method: 'DELETE' }),
  getPlayerTournamentHistory: (tag: string) =>
    request<PlayerTournamentHistory[]>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/tournaments`),

  // Игровые турниры (отслеживание турнира CR по тегу)
  getGameTournaments: () => request<GameTournament[]>('/api/game-tournaments'),
  getGameTournament: (id: number) => request<GameTournament>(`/api/game-tournaments/${id}`),
  addGameTournament: (tag: string, password?: string) =>
    request<GameTournament>('/api/game-tournaments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tag, password: password ?? null }),
    }),
  removeGameTournament: (id: number) =>
    request<{ ok: boolean }>(`/api/game-tournaments/${id}`, { method: 'DELETE' }),
}