import { initData } from './telegram'
import type { ClanHistory, ClanStatus, ClanWarLog, GlobalTop, MyStats, NudgeResult, OwnerClan, PlayerHistory, SeasonStats } from '../types'

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
  getGlobalTop: () => request<GlobalTop>('/api/players/top'),

  // Панель владельца
  ownerGetClans: () => request<OwnerClan[]>('/api/owner/clans'),
  ownerSetPlan: (clanId: number, tier: 'pro' | 'free', days?: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}/plan`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tier, days: days ?? null }),
    }),
  ownerDeleteClan: (clanId: number) =>
    request<{ ok: boolean }>(`/api/owner/clans/${clanId}`, { method: 'DELETE' }),
}