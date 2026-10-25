import { useEffect, useState } from 'react'
import { api } from './api'
import type { DailyQuests } from '../types'

/**
 * Задания дня на стороне приложения: отметки заданий «в приложении» (тренажёр,
 * фишка, подбор колоды) и память на день - видел ли человек сегодня задания и что
 * ему выпало, чтобы не дёргать сервер лишний раз.
 */

/** Свежее состояние заданий - карточка и меню перерисовываются по нему. */
export const QUESTS_CHANGED = 'cwst:quests-changed'

/** Задание из приложения засчитано только что - для тоста поверх любого экрана. */
export const QUEST_PINGED = 'cwst:quest-pinged'
export interface QuestPinged { q: DailyQuests; kind: AppQuest }

/** Открыть подбор колод из любого места - App ловит событие. */
export const OPEN_DECKS = 'cwst:open-decks'

export type AppQuest = 'trainer' | 'tip' | 'decks'

const TODAY_KEY = 'cwst_quests_today'
const SEEN_KEY = 'cwst_quests_seen_day'

interface Remembered { day: string; keys: string[]; done: string[]; completed: boolean }

/** Местная дата браузера, yyyy-MM-dd - как день на сервере, если пояс тот же. */
export function localDay(d = new Date()): string {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

function read(): Remembered | null {
  try {
    const raw = localStorage.getItem(TODAY_KEY)
    const v = raw ? JSON.parse(raw) as Remembered : null
    return v && v.day === localDay() ? v : null
  } catch { return null }
}

/** Запомнить задания дня и сообщить всем, кто их показывает. */
export function rememberQuests(q: DailyQuests) {
  try {
    const v: Remembered = { day: q.day, keys: q.items.map(i => i.key), done: q.items.filter(i => i.done).map(i => i.key), completed: q.completed }
    localStorage.setItem(TODAY_KEY, JSON.stringify(v))
  } catch { /* без памяти - просто спросим сервер ещё раз */ }
  window.dispatchEvent(new CustomEvent<DailyQuests>(QUESTS_CHANGED, { detail: q }))
}

/** Карточку заданий сегодня уже видели - точка «загляни» больше не нужна. */
export function markQuestsSeen() {
  try { localStorage.setItem(SEEN_KEY, localDay()) } catch { /* точка просто останется */ }
}

export function questsSeenToday(): boolean {
  try { return localStorage.getItem(SEEN_KEY) === localDay() } catch { return true }
}

/** Для меню: видели ли сегодня задания. Обновляется, как только карточку показали. */
export function useQuestsSeen(): boolean {
  const [seen, setSeen] = useState(questsSeenToday)
  useEffect(() => {
    const on = () => setSeen(questsSeenToday())
    window.addEventListener(QUESTS_CHANGED, on)
    return () => window.removeEventListener(QUESTS_CHANGED, on)
  }, [])
  return seen
}

/**
 * Сделал задание в приложении. Сервер решает, засчитать ли его; не дёргаем его, если
 * уже известно, что сегодня такого задания нет или оно закрыто.
 */
const pinged = new Set<string>()

export function questDone(kind: AppQuest) {
  const known = read()
  if (known && (!known.keys.includes(kind) || known.done.includes(kind))) return
  // Подбор колод перезагружается на каждый фильтр - отмечаем раз за запуск и день
  const once = `${localDay()}:${kind}`
  if (pinged.has(once)) return
  pinged.add(once)
  api.questPing(kind)
    .then(q => {
      rememberQuests(q)
      const now = q.items.find(i => i.key === kind)
      // Тост - только когда этим действием что-то закрылось
      if (q.justCompleted || (now?.done && !known?.done.includes(kind)))
        window.dispatchEvent(new CustomEvent<QuestPinged>(QUEST_PINGED, { detail: { q, kind } }))
    })
    .catch(() => { pinged.delete(once) /* не засчиталось - засчитаем при следующем действии */ })
}

export function openDecks() {
  window.dispatchEvent(new Event(OPEN_DECKS))
}
