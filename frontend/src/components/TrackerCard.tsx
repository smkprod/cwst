import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TrackerState } from '../types'
import { botStartLink, haptic, hapticNotify, openExternalLink, requestWriteAccess } from '../lib/telegram'
import { useT } from '../lib/i18n'
import { SectionHead } from './ui/Section'

/**
 * «Трекер боёв» — включатель над историей боёв.
 *
 * Трекер бесплатный: карточка захода в боте и история приходят всем. Плюс только
 * добавляет строки «против кого и чем играть», поэтому здесь нет ни замка, ни цены.
 */
export function TrackerCard({ state, onChange }: { state: TrackerState | null; onChange: (s: TrackerState) => void }) {
  const { t, lang } = useT()
  const s = t.trk
  const [busy, setBusy] = useState(false)

  if (!state) return null
  if (!state.available) {
    return (
      <section className="card trk-card">
        <SectionHead icon="target" tone="red" title={s.title} className="bt-head-flush" />
        <p className="muted small" style={{ margin: '6px 0 0' }}>{s.closed}</p>
      </section>
    )
  }

  const toggle = async () => {
    haptic('medium')
    // Без права писать в личку бот не достучится до того, кто ни разу не нажал «Старт»
    if (!state.enabled && !(await requestWriteAccess())) { hapticNotify('error'); return }
    setBusy(true)
    try {
      onChange(await api.setTracker(!state.enabled, -new Date().getTimezoneOffset(), lang))
      hapticNotify('success')
    } catch {
      hapticNotify('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card trk-card">
      <SectionHead icon="target" tone="red" title={s.title} className="bt-head-flush"
        info={<>
          <p>{s.promise}</p>
          <p>{s.note}</p>
          <p>{s.honesty}</p>
        </>}
        aside={<button
          className={`ba-switch ${state.enabled ? 'ba-switch-on' : ''}`}
          role="switch"
          aria-checked={state.enabled}
          aria-label={s.title}
          disabled={busy}
          onClick={toggle}
        >
          <span className="ba-switch-knob" />
        </button>} />
      <p className={`small trk-note bt-status ${state.enabled ? 'bt-status-on' : 'muted'}`}>{state.enabled ? s.enabled : s.promise}</p>
      {state.enabled && state.mutedToday && <p className="muted small trk-note">{s.mutedToday}</p>}
      {state.dmBlocked && (
        <p className="small trk-note">
          {s.dmBlocked}{' '}
          <button className="btn-mini" onClick={() => openExternalLink(botStartLink())}>{s.openBot}</button>
        </p>
      )}
    </section>
  )
}

/** Состояние трекера с перезагрузкой — для экранов, где карточка стоит отдельно от истории. */
export function useTrackerState() {
  const [state, setState] = useState<TrackerState | null>(null)
  const load = useCallback(() => { api.getTracker().then(setState).catch(() => setState(null)) }, [])
  useEffect(load, [load])
  return [state, setState] as const
}
