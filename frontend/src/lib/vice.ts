/**
 * Временная тема «Vice City» к выходу GTA VI. Включается сама до конца недели
 * релиза и сама же выключается: ничего убирать руками не нужно.
 */

/** Релиз GTA VI - 19 ноября, полночь по Киеву. */
export const VICE_RELEASE_UTC = Date.UTC(2026, 10, 18, 22, 0)

/** Тема держится неделю после релиза. */
export const VICE_END_UTC = Date.UTC(2026, 10, 26, 22, 0)

export const NEWS_CHANNEL = 'https://t.me/clanifycr'

export const viceActive = (now = Date.now()) => now < VICE_END_UTC

const HIDE_KEY = 'cwst.vice.banner.hidden'

export function viceBannerHidden(): boolean {
  try { return localStorage.getItem(HIDE_KEY) === '1' } catch { return false }
}

export function hideViceBanner() {
  try { localStorage.setItem(HIDE_KEY, '1') } catch { /* пусть так */ }
}
