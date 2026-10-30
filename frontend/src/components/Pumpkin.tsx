import { useEffect, useState, useSyncExternalStore } from 'react'
import { api } from '../lib/api'
import { useT } from '../lib/i18n'
import { hwDate, openHalloween } from '../lib/halloween'
import { haptic, hapticNotify } from '../lib/telegram'
import { PLUS_CHANGED } from '../lib/plusSheet'
import type { PumpkinFindResult, PumpkinStatus } from '../types'

/**
 * «Охота на тыквы»: 13 тыкв спрятаны по вкладкам, тыква сама решает, показываться
 * ли (охота открыта и ещё не найдена). Состояние одно на всё приложение - тыквы на
 * разных экранах и раздел в Хэллоуине видят одни и те же находки.
 */
let state: PumpkinStatus | null = null
let loading: Promise<void> | null = null
const subs = new Set<() => void>()
const emit = () => subs.forEach(f => f())

function load(): Promise<void> {
  loading ??= api.getPumpkins()
    .then(s => { state = s; emit() })
    .catch(() => { loading = null })
  return loading
}

export function usePumpkins(): PumpkinStatus | null {
  const s = useSyncExternalStore(cb => { subs.add(cb); return () => { subs.delete(cb) } }, () => state)
  useEffect(() => { void load() }, [])
  return s
}

const FOUND = 'cwst:pumpkin-found'
type Found = PumpkinFindResult & { id: number }

/** Следующая ненайденная - к ней ведёт подсказка призрака. */
export const nextPumpkin = (s: PumpkinStatus) => Array.from({ length: s.total }, (_, i) => i + 1).find(i => !s.found.includes(i)) ?? null

/**
 * Спрятанная тыква. Место не занимает: сидит поверх края соседней карточки и
 * качается, чтобы глаз её зацепил, но не сразу.
 */
export function Pumpkin({ id, side = 'right' }: { id: number; side?: 'left' | 'right' | 'mid' }) {
  const s = usePumpkins()
  const [busy, setBusy] = useState(false)
  if (!s?.open || s.found.includes(id)) return null

  const take = async () => {
    if (busy) return
    setBusy(true)
    haptic('heavy')
    try {
      const r = await api.findPumpkin(id)
      state = { ...s, found: r.found, finishers: s.finishers + (r.completed ? 1 : 0), hunters: s.found.length === 0 ? s.hunters + 1 : s.hunters }
      emit()
      if (r.completed) window.dispatchEvent(new Event(PLUS_CHANGED))
      window.dispatchEvent(new CustomEvent<Found>(FOUND, { detail: { ...r, id } }))
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={`pk-anchor pk-${side}`}>
      <button className="pk" onClick={take} aria-label="🎃">
        <img src="/hw/pumpkin-s.webp" alt="" />
      </button>
    </div>
  )
}

/** Окно «Тыква найдена!» - одно на приложение, открывается событием от тыквы. */
export function PumpkinHost() {
  const { t } = useT()
  const s = t.hw
  const st = usePumpkins()
  const [found, setFound] = useState<Found | null>(null)

  useEffect(() => {
    const on = (e: Event) => { setFound((e as CustomEvent<Found>).detail); hapticNotify('success') }
    window.addEventListener(FOUND, on)
    return () => window.removeEventListener(FOUND, on)
  }, [])

  if (!found || !st) return null
  const close = () => { haptic('light'); setFound(null) }
  const next = nextPumpkin(st)

  return (
    <div className="modal-backdrop pk-backdrop" onClick={close}>
      <div className="pk-modal fade-up" onClick={e => e.stopPropagation()} role="dialog" aria-modal="true">
        <div className="pk-rays" />
        <img className="pk-big" src="/hw/pumpkin.webp" alt="" />
        <h2>{found.isNew ? s.foundTitle : s.foundAgain}</h2>
        <div className="pk-count">{s.huntProgress.replace('{n}', String(found.foundCount)).replace('{total}', String(st.total))}</div>
        <PumpkinDots st={st} />
        {found.completed ? (
          <p className="pk-reward">{s.foundAll.replace('{days}', String(st.rewardPlusDays))}</p>
        ) : next && (
          <p className="pk-hint"><b>👻 {s.huntGhost}:</b> {s.huntHints[next - 1]}</p>
        )}
        <div className="pk-btns">
          <button className="btn pk-btn" onClick={close}>{found.completed ? t.ui.gotIt : s.foundNext}</button>
          <button className="btn btn-ghost pk-btn" onClick={() => { close(); openHalloween() }}>{s.foundToHunt}</button>
        </div>
      </div>
    </div>
  )
}

function PumpkinDots({ st }: { st: PumpkinStatus }) {
  return (
    <div className="pk-dots">
      {Array.from({ length: st.total }, (_, i) => (
        <span key={i} className={st.found.includes(i + 1) ? 'pk-dot pk-dot-on' : 'pk-dot'} />
      ))}
    </div>
  )
}

/** Раздел охоты во вкладке «Хэллоуин»: прогресс, подсказка призрака, награда, счёт. */
export function PumpkinHunt() {
  const { t } = useT()
  const s = t.hw
  const st = usePumpkins()
  if (!st?.open) return null
  const n = st.found.length
  const next = nextPumpkin(st)
  const done = n >= st.total

  return (
    <section className="hw-glass hw-rim-or pk-hunt">
      <div className="hw-h">
        <div className="hw-ico hw-ico-or"><img src="/hw/pumpkin-s.webp" alt="" /></div>
        <div className="hw-h-title">{s.huntTitle}</div>
        <span className="pk-hunt-n">{s.huntProgress.replace('{n}', String(n)).replace('{total}', String(st.total))}</span>
      </div>
      <p className="hw-muted">{s.huntLead}</p>
      <div className="pk-grid">
        {Array.from({ length: st.total }, (_, i) => (
          <img key={i} src="/hw/pumpkin-s.webp" alt="" className={st.found.includes(i + 1) ? 'pk-slot pk-slot-on' : 'pk-slot'} />
        ))}
      </div>
      <div className="hw-bar pk-bar"><i style={{ width: `${Math.round((100 * n) / st.total)}%` }} /></div>
      {done
        ? <p className="pk-reward">{s.huntDone.replace('{days}', String(st.rewardPlusDays))}</p>
        : next && (
          <div className="pk-ghost">
            <img src="/hw/ghost.webp" alt="" />
            <div>
              <div className="hw-muted-cap">{s.huntGhost}</div>
              <b>{s.huntHints[next - 1]}</b>
              {next >= 7 && next <= 11 && <div className="hw-muted">{s.huntHintTabs}</div>}
            </div>
          </div>
        )}
      {!done && <p className="hw-note">🎁 {s.huntReward.replace('{days}', String(st.rewardPlusDays))}</p>}
      <p className="hw-note">
        {s.huntStats.replace('{hunters}', String(st.hunters)).replace('{finishers}', String(st.finishers))}
        {st.firstFinishers.length > 0 && <> · {s.huntFirst.replace('{names}', st.firstFinishers.join(', '))}</>}
        {' · '}{s.huntEnds.replace('{date}', hwDate(st.endUtc, t.dateLocale))}
      </p>
    </section>
  )
}
