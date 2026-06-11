import { useState } from 'react'
import { api } from '../lib/api'
import { haptic, hapticNotify } from '../lib/telegram'

/** Настройка автонапоминаний (только для админа группы):
 *  за сколько часов до конца военного дня напоминать не доигравшим 4/4. */
export function ReminderCard({ initialHours }: { initialHours: number }) {
  const [hours, setHours] = useState(initialHours)
  const [saving, setSaving] = useState(false)
  const [savedAt, setSavedAt] = useState<number | null>(null)

  const options = [1, 2, 3, 4, 6, 8, 12]

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
        <div className="card-title">⏰ Автонапоминания</div>
        {savedAt !== null && <span className="trend-chip trend-ahead">✓ сохранено</span>}
      </div>
      <p className="muted small" style={{ margin: '0 0 10px' }}>
        Бот напомнит тем, кто не отыграл все 4/4 колоды, за {hours} ч до конца военного дня
        (день заканчивается в 10:00 UTC).
      </p>
      <div className="reminder-options">
        {options.map(h => (
          <button
            key={h}
            className={`btn-mini ${h === hours ? 'reminder-active' : ''}`}
            disabled={saving}
            onClick={() => save(h)}
          >
            {h} ч
          </button>
        ))}
      </div>
    </section>
  )
}
