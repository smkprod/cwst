import { api } from './api'
import type { HalloweenBossKey, HalloweenView } from '../types'

/**
 * Хэллоуин: общая загрузка босса для вкладки, баннера в клане и заставки. Три
 * места на одном экране - три одинаковых запроса, поэтому ответ живёт минуту.
 */
export const OPEN_HALLOWEEN = 'cwst:open-halloween'

const TTL_MS = 60_000
let cached: { at: number; promise: Promise<HalloweenView> } | null = null

export function loadHalloween(force = false): Promise<HalloweenView> {
  if (!force && cached && Date.now() - cached.at < TTL_MS) return cached.promise
  const promise = api.getHalloween()
  cached = { at: Date.now(), promise }
  // Ошибку не кэшируем: следующий заход попробует снова
  promise.catch(() => { if (cached?.promise === promise) cached = null })
  return promise
}

export function openHalloween() {
  window.dispatchEvent(new Event(OPEN_HALLOWEEN))
}

export const BOSS_ART: Record<HalloweenBossKey, string> = {
  vampire: '/hw/bat.webp',
  king: '/hw/king.webp',
}

/** «26 октября» на языке интерфейса, по Киеву - как в сообщениях бота. */
export function hwDate(utc: string, locale: string): string {
  return new Date(utc).toLocaleDateString(locale, { day: 'numeric', month: 'long', timeZone: 'Europe/Kyiv' })
}

export function hwNum(n: number, locale: string): string {
  return n.toLocaleString(locale).replace(/,/g, ' ')
}
