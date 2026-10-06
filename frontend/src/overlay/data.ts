import { useEffect, useLayoutEffect, useRef, useState, type RefObject } from 'react'
import { API_BASE } from '../lib/apiBase'
import type { Translations } from '../lib/i18n'
import type { Tournament, TournamentMatch, TournamentParticipant } from '../types'

/** Как часто виджет переспрашивает сервер. Чаще не нужно: счёт меняется раз в бой. */
export const POLL_MS = 5_000
const TIMEOUT_MS = 10_000

export type Poll<T> =
  | { kind: 'loading' }
  | { kind: 'missing' }
  | { kind: 'ready'; data: T }

/**
 * Опрос публичной ручки виджета.
 *
 * Обычный fetch без заголовков Telegram: OBS про Telegram ничего не знает, доступ
 * даёт ключ в адресе. Сбой сети не стирает картинку — виджет висит в эфире, и
 * пустая плашка посреди стрима хуже, чем счёт пятисекундной давности. Пропадает
 * он только на честный 404: ключ сменили или турнир удалили.
 */
export function usePoll<T>(path: string | null): Poll<T> {
  const [state, setState] = useState<Poll<T>>(path ? { kind: 'loading' } : { kind: 'missing' })
  // Сравниваем сырой ответ: одинаковые данные не должны перерисовывать виджет и
  // заново запускать анимации на каждом опросе.
  const last = useRef<string | null>(null)

  useEffect(() => {
    if (!path) return
    let alive = true
    let timer: number | undefined

    const tick = async () => {
      const ctrl = new AbortController()
      const abort = window.setTimeout(() => ctrl.abort(), TIMEOUT_MS)
      try {
        const res = await fetch(`${API_BASE}${path}`, { signal: ctrl.signal, cache: 'no-store' })
        if (!alive) return
        if (res.status === 404) {
          last.current = null
          setState({ kind: 'missing' })
        } else if (res.ok) {
          const text = await res.text()
          if (alive && text !== last.current) {
            last.current = text
            setState({ kind: 'ready', data: JSON.parse(text) as T })
          }
        }
        // Остальные ответы (5xx, 429) — временные: держим последнее, что было.
      } catch {
        /* сеть моргнула — держим последнее, следующий опрос через POLL_MS */
      } finally {
        window.clearTimeout(abort)
        if (alive) timer = window.setTimeout(tick, POLL_MS)
      }
    }

    void tick()
    return () => { alive = false; window.clearTimeout(timer) }
  }, [path])

  return state
}

/**
 * Плавная перестановка строк (FLIP): строка, обогнавшая соседа, едет на новое
 * место, а не прыгает. На стриме это и есть событие — «он вышел на первое место», —
 * и его должно быть видно.
 *
 * Координаты берём из offsetTop/offsetLeft, а не getBoundingClientRect: весь виджет
 * масштабируется под размер источника в OBS, и экранные пиксели не совпадали бы
 * с пикселями внутри сцены.
 */
export function useFlip(ref: RefObject<HTMLElement>, dep: unknown) {
  const prev = useRef(new Map<string, { x: number; y: number }>())
  useLayoutEffect(() => {
    const root = ref.current
    if (!root) return
    const next = new Map<string, { x: number; y: number }>()
    const first = prev.current.size === 0
    root.querySelectorAll<HTMLElement>('[data-flip]').forEach(node => {
      const key = node.dataset.flip!
      const pos = { x: node.offsetLeft, y: node.offsetTop }
      next.set(key, pos)
      const old = prev.current.get(key)
      if (old && (old.x !== pos.x || old.y !== pos.y)) {
        node.animate(
          [{ transform: `translate(${old.x - pos.x}px, ${old.y - pos.y}px)` }, { transform: 'none' }],
          { duration: 700, easing: 'cubic-bezier(.2, .8, .2, 1)' },
        )
      } else if (!old && !first) {
        // Новенький в таблице — проявляется, а не возникает из ниоткуда
        node.animate([{ opacity: 0, transform: 'translateY(14px)' }, { opacity: 1, transform: 'none' }],
          { duration: 450, easing: 'ease-out' })
      }
    })
    prev.current = next
  }, [dep, ref])
}

/** Имя стороны: в парном турнире — команда, иначе игрок. */
export const sideName = (p: TournamentParticipant | null) => p ? (p.teamName ?? p.playerName) : ''

/** Сколько побед нужно в матче: у финала формат может быть свой (как на сервере). */
export function winsNeeded(t: Tournament, m: TournamentMatch): number {
  return m.nextMatchId == null ? (t.finalBestOf ?? t.bestOf) : t.bestOf
}

/** bestOf у турнира — это число побед; зрителю привычнее «Bo3». */
export const boLabel = (wins: number) => `Bo${wins * 2 - 1}`

export const isLive = (m: TournamentMatch) => m.status === 'ready' && (m.scoreA > 0 || m.scoreB > 0)

/** Название раунда, считая от финала: так его называют комментаторы. */
export function roundName(round: number, maxRound: number, t: Translations): string {
  switch (maxRound - round) {
    case 0: return t.overlay.final
    case 1: return t.overlay.semi
    case 2: return t.overlay.quarter
    case 3: return t.overlay.eighth
    default: return t.overlay.round.replace('{n}', String(round))
  }
}

/**
 * Какой матч сейчас «текущий»: начатая серия (уже есть счёт), иначе первая готовая
 * пара в самом раннем раунде — её и будут играть следующей.
 */
export function currentMatch(t: Tournament): TournamentMatch | null {
  const ready = t.matches
    .filter(m => m.status === 'ready' && m.participantA && m.participantB)
    .sort((a, b) => a.round - b.round || a.slotIndex - b.slotIndex)
  return ready.find(isLive) ?? ready[0] ?? null
}

export const champion = (t: Tournament) =>
  t.status === 'completed' ? t.participants.find(p => p.finalPlacement === 1) ?? null : null

/** Живой обратный отсчёт: перерисовывается раз в секунду, независимо от опроса. */
export function useNow(stepMs = 1000): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), stepMs)
    return () => window.clearInterval(id)
  }, [stepMs])
  return now
}
