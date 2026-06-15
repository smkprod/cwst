import { useState } from 'react'
import { api } from '../lib/api'
import { haptic, hapticNotify } from '../lib/telegram'
import type { Plan } from '../types'
import { useT } from '../lib/i18n'

interface Props {
  initialHours: number
  plan: Plan
  linkedCount: number
}

const FREE_DM_LIMIT = 5

export function ReminderCard({ initialHours, plan, linkedCount }: Props) {
  const [hours, setHours] = useState(initialHours)
  const [saving, setSaving] = useState(false)
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const { t } = useT()

  const options = [1, 2, 3, 4, 6, 8, 12]
  const showUpsell = plan === 'free' && linkedCount > FREE_DM_LIMIT

  const save = async (h: number) => {
    haptic('light')
    const prev = hours
    setHours(h)
    setSaving(true)
    try {
      await api.setReminderHours(h)
      hapticNotify('success')
      setSavedAt(Date.now())
    } catch {
      hapticNotify('error')
      setHours(prev)
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card">
      <div className="card-title-row">
        <div className="card-title">{t.reminder.title}</div>
        {savedAt !== null && <span className="trend-chip trend-ahead">{t.reminder.saved}</span>}
      </div>
      <p className="muted small" style={{ margin: '0 0 10px' }}>
        {t.reminder.desc1}{hours} {t.reminder.h}{t.reminder.desc2}
      </p>
      <p className="muted small" style={{ margin: '0 0 10px' }}>
        {t.reminder.dmTitle} <strong>{t.reminder.dmFree}</strong> {t.reminder.dmFreeDesc} {FREE_DM_LIMIT} {t.reminder.dmFreeDescSuffix}{' '}
        <strong>{t.reminder.dmPro}</strong> {t.reminder.dmProDesc}
      </p>
      {showUpsell && (
        <p className="muted small reminder-upsell" style={{ margin: '0 0 10px' }}>
          {t.reminder.upsell1} {FREE_DM_LIMIT} {t.reminder.upsell2} {linkedCount} {t.reminder.upsell3}{' '}
          <span className="pro-chip">PRO</span>
        </p>
      )}
      <div className="reminder-options">
        {options.map(h => (
          <button
            key={h}
            className={`btn-mini ${h === hours ? 'reminder-active' : ''}`}
            disabled={saving}
            onClick={() => save(h)}
          >
            {h} {t.reminder.h}
          </button>
        ))}
      </div>
    </section>
  )
}
