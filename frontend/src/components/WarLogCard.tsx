import { useState } from 'react'
import type { WarLogWeek } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'

const PLACE_ICONS = ['🥇', '🥈', '🥉', '4', '5']

interface WeeksProps {
  weeks: WarLogWeek[]
  /** Бейдж у выделенного клана в раскрытой таблице ("мы"); undefined — без бейджа. */
  meLabel?: string
}

/** Список недель журнала: место выделенного клана; по тапу — полная таблица. */
export function WarLogWeeks({ weeks, meLabel }: WeeksProps) {
  const [openKey, setOpenKey] = useState<string | null>(null)

  const toggle = (key: string) => {
    haptic('light')
    setOpenKey(k => (k === key ? null : key))
  }

  return (
    <ul className="warlog-list">
      {weeks.map(w => {
        const key = `${w.seasonId}-${w.sectionIndex}`
        const ours = w.standings.find(s => s.isOurClan)
        const isOpen = openKey === key
        const placeText = !ours ? '—'
          : ours.rank === 1 ? '🥇 победа'
          : ours.rank <= 3 ? `${PLACE_ICONS[ours.rank - 1]} ${ours.rank}-е место`
          : `${ours.rank}-е место`

        return (
          <li key={key} className="warlog-week">
            <button className="warlog-row" onClick={() => toggle(key)}>
              <span className="history-week-badge">
                {w.isColosseum ? '🏛' : `W${w.sectionIndex + 1}`}
              </span>
              <div className="warlog-info">
                <span className="warlog-place">{placeText}</span>
                <span className="muted small">сезон {w.seasonId}</span>
              </div>
              <div className="warlog-numbers">
                {ours && <span className="race-fame">{fmt(ours.fame)} 🏅</span>}
                {ours && ours.trophyChange !== 0 && (
                  <span className={`warlog-trophy ${ours.trophyChange > 0 ? 'trophy-up' : 'trophy-down'}`}>
                    {ours.trophyChange > 0 ? '+' : ''}{ours.trophyChange} 🏆
                  </span>
                )}
              </div>
              <span className={`warlog-chevron ${isOpen ? 'warlog-chevron-open' : ''}`}>›</span>
            </button>

            {isOpen && (
              <ul className="warlog-standings fade-in">
                {w.standings.map(s => (
                  <li key={`${key}-${s.rank}`} className={`warlog-standing ${s.isOurClan ? 'race-ours' : ''}`}>
                    <span className={`race-pos ${s.rank === 1 ? 'race-pos-gold' : ''}`}>{s.rank}</span>
                    <span className="warlog-standing-name">
                      {s.name}
                      {s.isOurClan && meLabel && <span className="me-badge">{meLabel}</span>}
                    </span>
                    <span className="race-fame">{fmt(s.fame)}</span>
                    <span className={`warlog-trophy ${s.trophyChange > 0 ? 'trophy-up' : s.trophyChange < 0 ? 'trophy-down' : 'muted'}`}>
                      {s.trophyChange > 0 ? '+' : ''}{s.trophyChange}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </li>
        )
      })}
    </ul>
  )
}

interface Props {
  log: WarLogWeek[]
}

/** Журнал прошлых войн нашего клана: карточка на вкладке «Война». */
export function WarLogCard({ log }: Props) {
  if (!log || log.length === 0) return null

  return (
    <section className="card warlog-card">
      <div className="card-title-row">
        <div className="card-title">📜 Журнал войн</div>
        <span className="muted small">тапни неделю — все кланы</span>
      </div>
      <WarLogWeeks weeks={log} meLabel="мы" />
    </section>
  )
}
