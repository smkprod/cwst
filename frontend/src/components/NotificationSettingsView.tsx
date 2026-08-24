import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { BotLang, NotificationSettings, NotifyChannel } from '../types'
import { haptic, hapticNotify } from '../lib/telegram'
import { useT, type Translations } from '../lib/i18n'

/** Подписи те же, что у переключателя интерфейса: коротко и без флагов. */
const BOT_LANGS: BotLang[] = ['ru', 'uk', 'en']
const BOT_LANG_LABELS: Record<BotLang, string> = { ru: 'RU', uk: 'UA', en: 'EN' }

type State =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; s: NotificationSettings }

// Время конца КВ хранится на бэке как минуты от 00:00 UTC; в поле показываем ЛОКАЛЬНОЕ время.
function utcMinToLocalTime(utcMin: number | null): string {
  if (utcMin == null) return ''
  const d = new Date()
  d.setUTCHours(Math.floor(utcMin / 60), utcMin % 60, 0, 0)
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
function localTimeToUtcMin(time: string): number | null {
  if (!time) return null
  const [h, m] = time.split(':').map(Number)
  if (Number.isNaN(h) || Number.isNaN(m)) return null
  const d = new Date()
  d.setHours(h, m, 0, 0)
  return d.getUTCHours() * 60 + d.getUTCMinutes()
}

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

            {/* Язык сообщений — первым блоком: он влияет на все уведомления ниже.
                Это язык бота для всего клана, а не интерфейса: приложение каждый
                читает на своём (⚙️ Ещё → Язык), а в общий чат сообщение уходит одно. */}
            <div className="card notif-block">
              <div className="notif-row">
                <div className="notif-row-text">
                  <span className="notif-row-label">{t.notif.langTitle}</span>
                  <span className="muted small">{t.notif.langDesc}</span>
                </div>
              </div>
              <div className="notif-hours-btns" style={{ marginTop: 8 }}>
                {BOT_LANGS.map(code => (
                  <button
                    key={code}
                    className={`btn-mini ${state.s.language === code ? 'notif-on' : ''}`}
                    onClick={() => { haptic('light'); patch({ language: code }) }}
                    aria-pressed={state.s.language === code}
                  >
                    {BOT_LANG_LABELS[code]}
                  </button>
                ))}
              </div>
            </div>

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
                      {[1, 2, 3, 4, 5, 6].map(h => (
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

            {/* Во сколько заканчивается КВ (от этого времени считаются напоминания и звонок) */}
            <div className="card notif-block">
              <div className="notif-row">
                <div className="notif-row-text">
                  <span className="notif-row-label">{t.notif.warEndTitle}</span>
                  <span className="muted small">{t.notif.warEndDesc}</span>
                </div>
                <input
                  type="time"
                  className="notif-time"
                  value={utcMinToLocalTime(state.s.warEndMinuteUtc)}
                  onChange={e => patch({ warEndMinuteUtc: localTimeToUtcMin(e.target.value) })}
                />
              </div>
              <p className="muted small" style={{ margin: '8px 0 0' }}>
                {state.s.warEndMinuteUtc == null ? t.notif.warEndDefault : t.notif.warEndLocalHint}
              </p>
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

            {/* Поздравление «900 за день» */}
            <div className="card notif-block">
              <ToggleRow
                label={t.notif.perfectDayTitle}
                desc={t.notif.perfectDayDesc}
                on={state.s.perfectDayEnabled}
                onToggle={() => patch({ perfectDayEnabled: !state.s.perfectDayEnabled })}
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
