import type { DuelLeagueKey } from '../types'
import type { Translations } from './i18n'

/**
 * Ранги лиги дуэлей — зеркало DuelUseCase.RankOf на сервере. Шесть лиг, у каждой
 * кроме Легенды три дивизиона по 50 кубков: III → II → I.
 */
export const LEAGUES: DuelLeagueKey[] = ['bronze', 'silver', 'gold', 'diamond', 'master', 'legend']
export const LEAGUE_FLOORS = [0, 1000, 1150, 1300, 1450, 1600]
const SPAN = 50
const ROMAN = ['', 'I', 'II', 'III']

export interface Rank {
  league: DuelLeagueKey
  index: number
  /** 3, 2, 1; 0 — Легенда. */
  division: number
  floor: number
  next: number | null
}

export function rankOf(trophies: number): Rank {
  let i = 0
  while (i + 1 < LEAGUE_FLOORS.length && trophies >= LEAGUE_FLOORS[i + 1]) i++
  if (i === LEAGUES.length - 1) return { league: 'legend', index: i, division: 0, floor: LEAGUE_FLOORS[i], next: null }
  const top = LEAGUE_FLOORS[i + 1]
  const division = trophies >= top - SPAN ? 1 : trophies >= top - 2 * SPAN ? 2 : 3
  const floor = division === 1 ? top - SPAN : division === 2 ? top - 2 * SPAN : LEAGUE_FLOORS[i]
  const next = division === 1 ? top : top - (division - 1) * SPAN
  return { league: LEAGUES[i], index: i, division, floor, next }
}

export function rankName(league: DuelLeagueKey, division: number, t: Translations): string {
  const name = t.duel.leagues[league]
  return division > 0 ? `${name} ${ROMAN[division]}` : name
}

/** Все ранги по порядку — для лестницы: Бронза III … Легенда. */
export function allRanks(): { league: DuelLeagueKey; division: number; floor: number }[] {
  const out: { league: DuelLeagueKey; division: number; floor: number }[] = []
  LEAGUES.forEach((league, i) => {
    if (league === 'legend') { out.push({ league, division: 0, floor: LEAGUE_FLOORS[i] }); return }
    const top = LEAGUE_FLOORS[i + 1]
    out.push({ league, division: 3, floor: LEAGUE_FLOORS[i] })
    out.push({ league, division: 2, floor: top - 2 * SPAN })
    out.push({ league, division: 1, floor: top - SPAN })
  })
  return out
}

/** Цвета лиги: светлый, основной, тёмный и свечение — для эмблем и фона. */
export const LEAGUE_COLORS: Record<DuelLeagueKey, { hi: string; mid: string; lo: string; glow: string }> = {
  bronze: { hi: '#ffd2a8', mid: '#c27a43', lo: '#5e3016', glow: 'rgba(214, 132, 66, 0.55)' },
  silver: { hi: '#ffffff', mid: '#aab6ca', lo: '#4c586f', glow: 'rgba(196, 210, 236, 0.5)' },
  gold: { hi: '#fff3b0', mid: '#f3b52c', lo: '#8a5205', glow: 'rgba(255, 196, 56, 0.6)' },
  diamond: { hi: '#dcfdff', mid: '#3fc6f2', lo: '#15519b', glow: 'rgba(63, 198, 242, 0.6)' },
  master: { hi: '#f6dcff', mid: '#b25cf2', lo: '#3f1580', glow: 'rgba(178, 92, 242, 0.6)' },
  legend: { hi: '#fff2b8', mid: '#ff7a24', lo: '#a3101c', glow: 'rgba(255, 106, 36, 0.7)' },
}
