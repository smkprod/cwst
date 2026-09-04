import { initData } from './telegram'
import type { BroadcastResult, BroadcastTarget, ClanDiscipline, DailyPuzzle, ClanHistory, ClanOverview, ClanRanking, ClanStatus, ClanWarLog, DeckSuggestions, GameTournament, GlobalTop, LinkedPlayer, MyStats, NotificationSettings, NudgeResult, OwnerClan, OwnerClanDetail, OwnerStats, PlayerHistory, PlayerProfile, PlayerTournamentHistory, RaceScout, RecruitmentCandidates, RecruitmentStatus, Achievements, WhatsNew, RespectStatus, SeasonArchive, SeasonBreakdown, SeasonStats, Tournament, TournamentSummary, WarJournal } from '../types'

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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  // КРИТИЧЕСКИЙ ФИКС: Достаем свежайший initData из window прямо в секунду отправки запроса.
  // Теперь заголовок больше никогда не уйдет на сервер пустым.
  const liveInitData = window.Telegram?.WebApp?.initData ?? '';

  const ctrl = new AbortController()
  const timer = setTimeout(() => ctrl.abort(), REQUEST_TIMEOUT_MS)

  let res: Response
  try {
    res = await fetch(`${BASE}${path}`, {
      ...init,
      signal: ctrl.signal,
      headers: {
        'X-Telegram-Init-Data': liveInitData,
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
  getAppConfig: () => request<{ botUsername: string }>('/api/app/config'),
  getMyClanStatus: () => request<ClanStatus>('/api/clans/my/status'),
  getClanStatus: (tag: string) =>
    request<ClanStatus>(`/api/clans/${encodeURIComponent(tag.replace('#', ''))}/status`),
  getMyStats: () => request<MyStats>('/api/players/me/stats'),
  getPlayerHistory: (tag: string) =>
    request<PlayerHistory>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/history`),
  getPlayerProfile: (tag: string) =>
    request<PlayerProfile>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/profile`),
  getMe: () => request<LinkedPlayer>('/api/players/me'),
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
  getMyAchievements: () => request<Achievements>('/api/players/me/achievements'),
  // 204 (нет данных: не привязан / нет войны) — отдаём null, а не падаем на пустом теле
  getWhatsNew: async (): Promise<WhatsNew | null> => {
    const res = await fetch(`${BASE}/api/players/me/whats-new`, {
      headers: { 'X-Telegram-Init-Data': window.Telegram?.WebApp?.initData ?? '' },
    })
    if (!res.ok || res.status === 204) return null
    return res.json().catch(() => null)
  },
  getRespectStatus: () => request<RespectStatus>('/api/players/me/respect-status'),
  getDailyPuzzle: () => request<DailyPuzzle>('/api/game/daily'),
  guessPuzzle: (cardId: number) =>
    request<DailyPuzzle>('/api/game/daily/guess', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ cardId }),
    }),

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
  ownerGetStats: () => request<OwnerStats>('/api/owner/stats'),
  ownerGetClans: () => request<OwnerClan[]>('/api/owner/clans'),
  ownerGetClanDetail: (clanId: number) => request<OwnerClanDetail>(`/api/owner/clans/${clanId}`),
  ownerSetPlan: (clanId: number, tier: 'pro' | 'free', days?: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}/plan`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tier, days: days ?? null }),
    }),
  ownerDeleteClan: (clanId: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}`, { method: 'DELETE' }),
  ownerBroadcast: (text: string, target: BroadcastTarget) =>
    request<BroadcastResult>('/api/owner/broadcast', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text, target }),
    }),

  // Турниры
  getTournaments: () => request<TournamentSummary[]>('/api/tournaments'),
  getTournament: (id: number) => request<Tournament>(`/api/tournaments/${id}`),
  createTournament: (req: {
    name: string; description?: string; prizeInfo?: string
    clanInviteLink: string; bestOf: number; minParticipants: number; maxParticipants: number
  }) =>
    request<Tournament>('/api/tournaments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  updateTournament: (id: number, req: {
    name: string; description?: string; prizeInfo?: string
    clanInviteLink: string; bestOf: number; minParticipants: number; maxParticipants: number
  }) =>
    request<Tournament>(`/api/tournaments/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  joinTournament: (id: number) =>
    request<Tournament>(`/api/tournaments/${id}/join`, { method: 'POST' }),
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