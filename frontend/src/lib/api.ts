import { initData } from './telegram'
import type { ClanHistory, ClanStatus, ClanWarLog, GlobalTop, MyStats, NudgeResult, OwnerClan, OwnerStats, PlayerHistory, PlayerProfile, PlayerTournamentHistory, RecruitmentCandidates, RecruitmentStatus, SeasonBreakdown, SeasonStats, Tournament, TournamentSummary } from '../types'

// Если мы на Render (production), BASE должен быть пустой строкой '', чтобы запросы шли на тот же домен.
// Для локальной разработки (Development) оставляем localhost:5000.
const BASE = import.meta.env.DEV 
  ? (import.meta.env.VITE_API_URL ?? 'http://localhost:5000') 
  : '';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  // КРИТИЧЕСКИЙ ФИКС: Достаем свежайший initData из window прямо в секунду отправки запроса.
  // Теперь заголовок больше никогда не уйдет на сервер пустым.
  const liveInitData = window.Telegram?.WebApp?.initData ?? '';

  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: { 
      'X-Telegram-Init-Data': liveInitData, 
      ...init?.headers 
    },
  })
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
  getMyClanStatus: () => request<ClanStatus>('/api/clans/my/status'),
  getClanStatus: (tag: string) =>
    request<ClanStatus>(`/api/clans/${encodeURIComponent(tag.replace('#', ''))}/status`),
  getMyStats: () => request<MyStats>('/api/players/me/stats'),
  getPlayerHistory: (tag: string) =>
    request<PlayerHistory>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/history`),
  getPlayerProfile: (tag: string) =>
    request<PlayerProfile>(`/api/players/${encodeURIComponent(tag.replace('#', ''))}/profile`),
  getClanWarLog: (tag: string) =>
    request<ClanWarLog>(`/api/clans/${encodeURIComponent(tag.replace('#', ''))}/warlog`),
  nudgeSlackers: () => request<NudgeResult>('/api/clans/my/nudge', { method: 'POST' }),
  setReminderHours: (hoursBeforeEnd: number) =>
    request<{ ok: boolean; reminderHoursBeforeEnd: number }>('/api/clans/my/reminder', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ hoursBeforeEnd }),
    }),
  getMyClanHistory: (weeks = 8) => request<ClanHistory>(`/api/clans/my/history?weeks=${weeks}`),
  getMyClanSeason: () => request<SeasonStats>('/api/clans/my/season'),
  getSeasonBreakdown: () => request<SeasonBreakdown>('/api/clans/my/season-weeks'),
  getGlobalTop: () => request<GlobalTop>('/api/players/top'),

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
  ownerSetPlan: (clanId: number, tier: 'pro' | 'free', days?: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}/plan`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tier, days: days ?? null }),
    }),
  ownerDeleteClan: (clanId: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}`, { method: 'DELETE' }),

  // Турниры
  getTournaments: () => request<TournamentSummary[]>('/api/tournaments'),
  getTournament: (id: number) => request<Tournament>(`/api/tournaments/${id}`),
  createTournament: (req: {
    name: string; description?: string; prizeInfo?: string
    clanInviteLink: string; bestOf: number; maxParticipants: number
  }) =>
    request<Tournament>('/api/tournaments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    }),
  updateTournament: (id: number, req: {
    name: string; description?: string; prizeInfo?: string
    clanInviteLink: string; bestOf: number; maxParticipants: number
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
}