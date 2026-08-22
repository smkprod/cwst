import type { SearchHistoryItem } from '../types'

/**
 * История поиска. Живёт только на устройстве: кого человек смотрел — это его дело,
 * серверу такие данные не нужны, и хранить их там значило бы собирать лишнее.
 *
 * Любое обращение к localStorage обёрнуто: в приватном режиме и при запрете
 * хранилища он бросает исключение, а история — приятная мелочь, из-за которой
 * поиск падать не должен.
 */

const KEY = 'searchHistory'
const LIMIT = 12

export function readHistory(): SearchHistoryItem[] {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) return []
    // Пришло что-то не то (старая версия, ручная правка) — молча игнорируем запись
    return parsed.filter((x): x is SearchHistoryItem =>
      x && (x.kind === 'player' || x.kind === 'clan') &&
      typeof x.tag === 'string' && typeof x.name === 'string')
  } catch {
    return []
  }
}

/** Добавляет запись наверх, схлопывая повтор того же тега. Возвращает новый список. */
export function pushHistory(item: Omit<SearchHistoryItem, 'at'>): SearchHistoryItem[] {
  const next = [
    { ...item, at: Date.now() },
    ...readHistory().filter(x => !(x.kind === item.kind && x.tag === item.tag)),
  ].slice(0, LIMIT)

  try { localStorage.setItem(KEY, JSON.stringify(next)) } catch { /* приватный режим */ }
  return next
}

export function clearHistory(): SearchHistoryItem[] {
  try { localStorage.removeItem(KEY) } catch { /* приватный режим */ }
  return []
}
