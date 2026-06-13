import { useState } from 'react'
import type { WarLogWeek } from '../types'
import { fmt } from '../lib/format'
import { haptic } from '../lib/telegram'

const PLACE_ICONS = ['🥇', '🥈', '🥉', '4', '5']

interface WeeksProps {
  weeks: WarLogWeek[]
  meLabel?: string
}

export function WarLogWeeks({ weeks, meLabel }: WeeksProps) {
  const [openKey, setOpenKey] = useState<string | null>(null)
  const [openClan, setOpenClan] = useState<string | null>(null)

  const toggleWeek = (key: string) => {
    haptic('light')
    setOpenKey(k => (k === key ? null : key))
    setOpenClan(null)
  }

  const toggleClan = (clanKey: string) => {
    haptic('light')
    setOpenClan(k => (k === clanKey ? null : clanKey))
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
            <button className="warlog-row" onClick={() => toggleWeek(key)}>
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
                {w.standings.map(s => {
                  const clanKey = `${key}-${s.rank}`
                  const hasPlayers = s.players && s.players.length > 0
                  const isExpanded = openClan === clanKey
                  return (
                    <li key={clanKey} className={`warlog-standing ${s.isOurClan ? 'race-ours' : ''}`}>
                      <button
                        className={`warlog-clan-row ${hasPlayers ? '' : 'warlog-clan-row-plain'}`}
                        onClick={() => hasPlayers && toggleClan(clanKey)}
                        disabled={!hasPlayers}
                      >
                        <span className={`race-pos ${s.rank === 1 ? 'race-pos-gold' : ''}`}>{s.rank}</span>
                        <span className="warlog-standing-name">
                          {s.name}
                          {s.isOurClan && meLabel && <span className="me-badge">{meLabel}</span>}
                        </span>
                        <span className="race-fame">{fmt(s.fame)}</span>
                        <span className={`warlog-trophy ${s.trophyChange > 0 ? 'trophy-up' : s.trophyChange < 0 ? 'trophy-down' : 'muted'}`}>
                          {s.trophyChange > 0 ? '+' : ''}{s.trophyChange}
                        </span>
                        {hasPlayers && (
                          <span className={`warlog-chevron ${isExpanded ? 'warlog-chevron-open' : ''}`} style={{ fontSize: 12 }}>›</span>
                        )}
                      </button>

                      {isExpanded && hasPlayers && (
                        <ul className="warlog-players fade-in">
                          {s.players!.map((p, i) => (
                            <li key={p.name} className="warlog-player-row">
                              <span className="warlog-player-rank">#{i + 1}</span>
                              <span className="warlog-player-name">{p.name}</span>
                              <span className="muted small">{p.decksUsed > 0 ? `${p.decksUsed}🃏` : ''}</span>
                              <span className="race-fame">{fmt(p.fame)} 🏅</span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </li>
                  )
                })}
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
