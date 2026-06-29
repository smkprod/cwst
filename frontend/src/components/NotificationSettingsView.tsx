import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { NotificationSettings, NotifyChannel } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; s: NotificationSettings }

export function NotificationSettingsView({ onClose }: { onClose: () => void }) {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const { t } = useT()

  useEffect(() => {
    api.getNotificationSettings()
      .then(s => setState({ kind: 'ready', s }))
      .catch(() => setState({ kind: 'error' }))
  }, [])

  const patch = (p: Partial<NotificationSettings>) => {
    setState(st => st.kind === 'ready' ? { kind: 'ready', s: { ...st.s, ...p } } : st)
    setSaved(false)
  }

  const save = async () => {
    if (state.kind !== 'ready') return
    haptic('medium')
    setSaving(true)
    try {
      const updated = await api.setNotificationSettings(state.s)
      hapticNotify('success')
      setState({ kind: 'ready', s: updated })
      setSaved(true)
    } catch {
      hapticNotify('error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="notif-overlay" role="dialog" aria-modal="true">
      <div className="notif-sheet fade-in">
        <div className="notif-head">
          <h2>{t.notif.title}</h2>
          <button className="notif-close" onClick={onClose} aria-label="✕">✕</button>
        </div>

        {state.kind === 'loading' && <div className="center"><div className="spinner" /></div>}
        {state.kind === 'error' && <p className="center muted">{t.notif.error}</p>}

        {state.kind === 'ready' && (
          <>
            <p className="muted small notif-intro">{t.notif.intro}</p>

            {/* Напоминания не отыгравшим */}
            <div className="card notif-block">
              <ToggleRow
                label={t.notif.remindersTitle}
                desc={t.notif.remindersDesc}
                on={state.s.remindersEnabled}
                onToggle={() => patch({ remindersEnabled: !state.s.remindersEnabled })}
              />
              {state.s.remindersEnabled && (
                <>
                  <ChannelRow
                    value={state.s.remindersChannel}
                    onChange={c => patch({ remindersChannel: c })}
                    t={t}
                  />
                  <div className="notif-hours">
                    <span className="muted small">{t.notif.hoursLabel}</span>
                    <div className="notif-hours-btns">
                      {[1, 2, 3, 6, 12].map(h => (
                        <button
                          key={h}
                          className={`btn-mini ${state.s.reminderHoursBeforeEnd === h ? 'notif-on' : ''}`}
                          onClick={() => { haptic('light'); patch({ reminderHoursBeforeEnd: h }) }}
                        >
                          {h} {t.notif.hoursUnit}
                        </button>
                      ))}
                    </div>
                  </div>
                </>
              )}
            </div>

            {/* Анонс начала КВ */}
            <div className="card notif-block">
              <ToggleRow
                label={t.notif.warStartTitle}
                desc={t.notif.warStartDesc}
                on={state.s.warStartEnabled}
                onToggle={() => patch({ warStartEnabled: !state.s.warStartEnabled })}
              />
              {state.s.warStartEnabled && (
                <ChannelRow
                  value={state.s.warStartChannel}
                  onChange={c => patch({ warStartChannel: c })}
                  t={t}
                />
              )}
            </div>

            {/* Финальный пинок */}
            <div className="card notif-block">
              <ToggleRow
                label={t.notif.finalCallTitle}
                desc={t.notif.finalCallDesc}
                on={state.s.finalCallEnabled}
                onToggle={() => patch({ finalCallEnabled: !state.s.finalCallEnabled })}
              />
            </div>

            {/* Ежедневный отчёт */}
            <div className="card notif-block">
              <ToggleRow
                label={t.notif.dailyReportTitle}
                desc={t.notif.dailyReportDesc}
                on={state.s.dailyReportEnabled}
                onToggle={() => patch({ dailyReportEnabled: !state.s.dailyReportEnabled })}
              />
            </div>

            <button className="btn btn-nudge notif-save" disabled={saving} onClick={save}>
              {saving ? t.notif.saving : saved ? t.notif.saved : t.notif.save}
            </button>
          </>
        )}
      </div>
    </div>
  )
}

function ToggleRow(
  { label, desc, on, onToggle }:
  { label: string; desc: string; on: boolean; onToggle: () => void },
) {
  return (
    <div className="notif-row">
      <div className="notif-row-text">
        <span className="notif-row-label">{label}</span>
        <span className="muted small">{desc}</span>
      </div>
      <button
        className={`notif-switch ${on ? 'notif-switch-on' : ''}`}
        role="switch"
        aria-checked={on}
        onClick={() => { haptic('light'); onToggle() }}
      >
        <span className="notif-switch-knob" />
      </button>
    </div>
  )
}

function ChannelRow(
  { value, onChange, t }:
  { value: NotifyChannel; onChange: (c: NotifyChannel) => void; t: Translations },
) {
  const opts: { key: NotifyChannel; label: string }[] = [
    { key: 'dm', label: t.notif.chDm },
    { key: 'chat', label: t.notif.chChat },
    { key: 'both', label: t.notif.chBoth },
  ]
  return (
    <div className="notif-channels">
      {opts.map(o => (
        <button
          key={o.key}
          className={`btn-mini ${value === o.key ? 'notif-on' : ''}`}
          onClick={() => { haptic('light'); onChange(o.key) }}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}
