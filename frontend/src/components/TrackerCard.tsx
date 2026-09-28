import { useCallback, useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { TrackerState } from '../types'
import { botStartLink, haptic, hapticNotify, openExternalLink, requestWriteAccess } from '../lib/telegram'
import { useT } from '../lib/i18n'

/**
 * «🎯 Трекер боёв» — включатель над историей боёв.
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
        <div className="card-title" style={{ margin: 0 }}>{s.title}</div>
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
      <div className="tilt-toggle" style={{ marginTop: 0 }}>
        <div className="tilt-toggle-text">
          <b>{s.title}</b>
          <span className="muted small">{state.enabled ? s.enabled : s.promise}</span>
        </div>
        <button
          className={`ba-switch ${state.enabled ? 'ba-switch-on' : ''}`}
          role="switch"
          aria-checked={state.enabled}
          aria-label={s.title}
          disabled={busy}
          onClick={toggle}
        >
          <span className="ba-switch-knob" />
        </button>
      </div>
      {state.enabled && <p className="muted small trk-note">{s.note}</p>}
      {state.enabled && state.mutedToday && <p className="muted small trk-note">{s.mutedToday}</p>}
      {state.dmBlocked && (
        <p className="small trk-note">
          {s.dmBlocked}{' '}
          <button className="btn-mini" onClick={() => openExternalLink(botStartLink())}>{s.openBot}</button>
        </p>
      )}
      <p className="muted small trk-note">{s.honesty}</p>
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
